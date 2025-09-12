using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Trecom.Backend.Data;
using Trecom.Backend.Dto.Projects;
using Trecom.Backend.Models;
using Trecom.Backend.Services;

namespace Trecom.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProjectsController : ControllerBase
{
    private readonly TrecomDbContext _db;

    public ProjectsController(TrecomDbContext db) => _db = db;

    // POST: api/projects  -> create head + version 1 (+ participants v1)
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProjectDto dto)
    {
        var now = DateTime.UtcNow;
        var effectiveAt = dto.EffectiveAt ?? now;

        var status = await _db.Set<ProjectStatus>().FirstOrDefaultAsync(s => s.Id == dto.StatusId);
        var probability = status?.AutoProbabilityPercent ?? dto.ProbabilityPercent;

        var head = new ProjectHead { Id = Guid.NewGuid(), CreatedAt = now };

        var rev = new ProjectRevision
        {
            ProjectId = head.Id,
            Version = 1,
            EffectiveAt = effectiveAt,
            AuthorId = GetCurrentUserIdOrSystem(),

            AMId = dto.AMId,
            ClientId = dto.ClientId,
            MarketId = dto.MarketId,
            Name = dto.Name,
            StatusId = dto.StatusId,

            Value = dto.Value,
            Margin = dto.Margin,
            ProbabilityPercent = probability,

            DueQuarter = QuarterNormalizer.Normalize(dto.DueQuarter),
            InvoiceMonth = QuarterNormalizer.NormalizeYearMonth(dto.InvoiceMonth),
            PaymentQuarter = QuarterNormalizer.Normalize(dto.PaymentQuarter),

            VendorId = dto.VendorId,
            ArchitectureId = dto.ArchitectureId,

            Comment = dto.Comment,
            IsCanceled = false,
            CreatedAt = now
        };

        _db.Add(head);
        _db.Add(rev);

        // uczestnicy v1
        var participants = (dto.Participants ?? new List<Guid>())
            .Distinct()
            .Select(uid => new ProjectParticipantRev { ProjectId = head.Id, Version = 1, UserId = uid, IsOwner = false });
        _db.AddRange(participants);

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCurrentById), new { projectId = head.Id }, head.Id);
    }

    // POST: api/projects/{projectId}/revisions  -> append new version (+ participants snapshot)
    [HttpPost("{projectId:guid}/revisions")]
    public async Task<ActionResult<int>> AddRevision(Guid projectId, [FromBody] AddProjectRevisionDto dto)
    {
        var now = DateTime.UtcNow;
        var effectiveAt = dto.EffectiveAt ?? now;

        var headExists = await _db.Set<ProjectHead>().AnyAsync(h => h.Id == projectId);
        if (!headExists) return NotFound("Project does not exist.");

        var lastVersion = await _db.Set<ProjectRevision>()
            .Where(r => r.ProjectId == projectId)
            .MaxAsync(r => (int?)r.Version) ?? 0;

        var status = await _db.Set<ProjectStatus>().FirstOrDefaultAsync(s => s.Id == dto.StatusId);
        var probability = status?.AutoProbabilityPercent ?? dto.ProbabilityPercent;

        var rev = new ProjectRevision
        {
            ProjectId = projectId,
            Version = lastVersion + 1,
            EffectiveAt = effectiveAt,
            AuthorId = GetCurrentUserIdOrSystem(),

            AMId = dto.AMId,
            ClientId = dto.ClientId,
            MarketId = dto.MarketId,
            Name = dto.Name,
            StatusId = dto.StatusId,

            Value = dto.Value,
            Margin = dto.Margin,
            ProbabilityPercent = probability,

            DueQuarter = QuarterNormalizer.Normalize(dto.DueQuarter),
            InvoiceMonth = QuarterNormalizer.NormalizeYearMonth(dto.InvoiceMonth),
            PaymentQuarter = QuarterNormalizer.Normalize(dto.PaymentQuarter),

            VendorId = dto.VendorId,
            ArchitectureId = dto.ArchitectureId,

            Comment = dto.Comment,
            IsCanceled = dto.IsCanceled,
            CreatedAt = now
        };

        _db.Add(rev);

        // uczestnicy dla nowej rewizji:
        if (dto.Participants is { Count: > 0 })
        {
            var newSet = dto.Participants.Distinct()
                .Select(uid => new ProjectParticipantRev { ProjectId = projectId, Version = rev.Version, UserId = uid, IsOwner = false });
            _db.AddRange(newSet);
        }
        else
        {
            // sklonuj z poprzedniej rewizji (jeśli była)
            if (lastVersion > 0)
            {
                var prev = await _db.ProjectParticipantsRev
                    .Where(p => p.ProjectId == projectId && p.Version == lastVersion)
                    .ToListAsync();

                var cloned = prev.Select(p => new ProjectParticipantRev
                {
                    ProjectId = projectId,
                    Version = rev.Version,
                    UserId = p.UserId,
                    IsOwner = p.IsOwner
                });
                _db.AddRange(cloned);
            }
        }

        await _db.SaveChangesAsync();
        return Ok(rev.Version);
    }

    // GET: api/projects/current  (lista bieżących wersji z filtrami)
    [HttpGet("current")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetCurrent(
        [FromQuery] Guid? amId,
        [FromQuery] Guid? userId,              // pokaż projekty gdzie user jest uczestnikiem
        [FromQuery] int? marketId,
        [FromQuery] int? statusId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? vendorId,
        [FromQuery] int? architectureId,
        [FromQuery] string? dueQuarter,
        [FromQuery] string? invoiceMonth,
        [FromQuery] bool hideCanceled = true,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        // pobierz z widoku projects_current
        var current = _db.ProjectRevisions.FromSqlRaw("SELECT * FROM projects_current").AsQueryable();

        // filtry słownikowe
        if (hideCanceled) current = current.Where(r => !r.IsCanceled);
        if (amId.HasValue) current = current.Where(r => r.AMId == amId);
        if (marketId.HasValue) current = current.Where(r => r.MarketId == marketId);
        if (statusId.HasValue) current = current.Where(r => r.StatusId == statusId);
        if (clientId.HasValue) current = current.Where(r => r.ClientId == clientId);
        if (vendorId.HasValue) current = current.Where(r => r.VendorId == vendorId);
        if (architectureId.HasValue) current = current.Where(r => r.ArchitectureId == architectureId);
        if (!string.IsNullOrWhiteSpace(dueQuarter)) current = current.Where(r => r.DueQuarter == dueQuarter);
        if (!string.IsNullOrWhiteSpace(invoiceMonth)) current = current.Where(r => r.InvoiceMonth == invoiceMonth);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            current = current.Where(r => r.Name.ToLower().Contains(s));
        }

        // filtr po uczestniku (userId)
        if (userId.HasValue)
        {
            var joined = from r in current
                         join p in _db.ProjectParticipantsRev
                           on new { r.ProjectId, r.Version } equals new { p.ProjectId, p.Version }
                         group p by r into grp
                         select new { Revision = grp.Key, Participants = grp };

            joined = joined.Where(x => x.Participants.Any(p => p.UserId == userId));

            var list = await joined
                .Select(x => ToDtoWithParticipants(x.Revision, x.Participants.Select(p => p.UserId)))
                .OrderByDescending(d => d.EffectiveAt)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 500))
                .ToListAsync();

            return Ok(list);
        }
        else
        {
            var items = await current
                .OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.Version)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 500))
                .Select(r => ToDtoWithParticipants(r, Enumerable.Empty<Guid>()))
                .ToListAsync();

            // dociągnij uczestników (drugi strzał, żeby nie robić złożonego join’a):
            var keys = items.Select(i => new { i.ProjectId, i.Version }).ToList();
            var parts = await _db.ProjectParticipantsRev
                .Where(p => keys.Contains(new { p.ProjectId, p.Version }))
                .GroupBy(p => new { p.ProjectId, p.Version })
                .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.UserId).ToList());

            foreach (var i in items)
                if (parts.TryGetValue(new { i.ProjectId, i.Version }, out var users))
                    i.Participants = users;

            return Ok(items);
        }
    }

    // GET: api/projects?asOf=YYYY-MM-DD  (stan na dzień X, z filtrami jak wyżej)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetAsOf([FromQuery] DateTime? asOf,
        [FromQuery] Guid? amId, [FromQuery] Guid? userId, [FromQuery] int? marketId,
        [FromQuery] int? statusId, [FromQuery] Guid? clientId, [FromQuery] Guid? vendorId,
        [FromQuery] int? architectureId, [FromQuery] string? dueQuarter,
        [FromQuery] string? invoiceMonth, [FromQuery] bool hideCanceled = true)
    {
        var ts = asOf?.ToUniversalTime() ?? DateTime.UtcNow;

        var last = _db.ProjectRevisions
            .Where(r => r.EffectiveAt <= ts)
            .GroupBy(r => r.ProjectId)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.Version).First());

        if (hideCanceled) last = last.Where(r => !r.IsCanceled);
        if (amId.HasValue) last = last.Where(r => r.AMId == amId);
        if (marketId.HasValue) last = last.Where(r => r.MarketId == marketId);
        if (statusId.HasValue) last = last.Where(r => r.StatusId == statusId);
        if (clientId.HasValue) last = last.Where(r => r.ClientId == clientId);
        if (vendorId.HasValue) last = last.Where(r => r.VendorId == vendorId);
        if (architectureId.HasValue) last = last.Where(r => r.ArchitectureId == architectureId);
        if (!string.IsNullOrWhiteSpace(dueQuarter)) last = last.Where(r => r.DueQuarter == dueQuarter);
        if (!string.IsNullOrWhiteSpace(invoiceMonth)) last = last.Where(r => r.InvoiceMonth == invoiceMonth);

        // jeśli userId -> tylko projekty, gdzie jest uczestnikiem
        if (userId.HasValue)
        {
            var joined = from r in last
                         join p in _db.ProjectParticipantsRev
                           on new { r.ProjectId, r.Version } equals new { p.ProjectId, p.Version }
                         group p by r into grp
                         select new { Revision = grp.Key, Participants = grp };

            var filtered = joined.Where(x => x.Participants.Any(p => p.UserId == userId));
            var data = await filtered.Select(x => ToDtoWithParticipants(x.Revision, x.Participants.Select(p => p.UserId))).ToListAsync();
            return Ok(data);
        }
        else
        {
            var res = await last.Select(r => ToDtoWithParticipants(r, Enumerable.Empty<Guid>())).ToListAsync();

            var keys = res.Select(i => new { i.ProjectId, i.Version }).ToList();
            var parts = await _db.ProjectParticipantsRev
                .Where(p => keys.Contains(new { p.ProjectId, p.Version }))
                .GroupBy(p => new { p.ProjectId, p.Version })
                .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.UserId).ToList());
            foreach (var i in res)
                if (parts.TryGetValue(new { i.ProjectId, i.Version }, out var users))
                    i.Participants = users;

            return Ok(res);
        }
    }

    // GET: api/projects/{projectId}/history
    [HttpGet("{projectId:guid}/history")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetHistory(Guid projectId)
    {
        var list = await _db.ProjectRevisions
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Version)
            .Select(r => ToDtoWithParticipants(r, Enumerable.Empty<Guid>()))
            .ToListAsync();

        if (list.Count == 0) return NotFound();

        var keys = list.Select(i => new { i.ProjectId, i.Version }).ToList();
        var parts = await _db.ProjectParticipantsRev
            .Where(p => keys.Contains(new { p.ProjectId, p.Version }))
            .GroupBy(p => new { p.ProjectId, p.Version })
            .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.UserId).ToList());
        foreach (var i in list)
            if (parts.TryGetValue(new { i.ProjectId, i.Version }, out var users))
                i.Participants = users;

        return Ok(list);
    }

    // GET: api/projects/{projectId}/current
    [HttpGet("{projectId:guid}/current")]
    public async Task<ActionResult<ProjectDto>> GetCurrentById(Guid projectId)
    {
        var rev = await _db.ProjectRevisions
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.Version)
            .FirstOrDefaultAsync();

        if (rev is null) return NotFound();

        var dto = ToDtoWithParticipants(rev, Enumerable.Empty<Guid>());
        dto.Participants = await _db.ProjectParticipantsRev
            .Where(p => p.ProjectId == projectId && p.Version == rev.Version)
            .Select(p => p.UserId)
            .ToListAsync();

        return Ok(dto);
    }

    // GET: api/projects/summary  -> sumy dla tych samych filtrów co lista
    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary(
        [FromQuery] DateTime? asOf,
        [FromQuery] Guid? amId,
        [FromQuery] Guid? userId,
        [FromQuery] int? marketId,
        [FromQuery] int? statusId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? vendorId,
        [FromQuery] int? architectureId,
        [FromQuery] string? dueQuarter,
        [FromQuery] string? invoiceMonth,
        [FromQuery] bool hideCanceled = true)
    {
        IQueryable<ProjectRevision> baseQ;

        if (asOf.HasValue)
        {
            var ts = asOf.Value.ToUniversalTime();
            baseQ = _db.ProjectRevisions
                .Where(r => r.EffectiveAt <= ts)
                .GroupBy(r => r.ProjectId)
                .Select(g => g.OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.Version).First());
        }
        else
        {
            baseQ = _db.ProjectRevisions.FromSqlRaw("SELECT * FROM projects_current");
        }

        if (hideCanceled) baseQ = baseQ.Where(r => !r.IsCanceled);
        if (amId.HasValue) baseQ = baseQ.Where(r => r.AMId == amId);
        if (marketId.HasValue) baseQ = baseQ.Where(r => r.MarketId == marketId);
        if (statusId.HasValue) baseQ = baseQ.Where(r => r.StatusId == statusId);
        if (clientId.HasValue) baseQ = baseQ.Where(r => r.ClientId == clientId);
        if (vendorId.HasValue) baseQ = baseQ.Where(r => r.VendorId == vendorId);
        if (architectureId.HasValue) baseQ = baseQ.Where(r => r.ArchitectureId == architectureId);
        if (!string.IsNullOrWhiteSpace(dueQuarter)) baseQ = baseQ.Where(r => r.DueQuarter == dueQuarter);
        if (!string.IsNullOrWhiteSpace(invoiceMonth)) baseQ = baseQ.Where(r => r.InvoiceMonth == invoiceMonth);

        if (userId.HasValue)
        {
            var joined = from r in baseQ
                         join p in _db.ProjectParticipantsRev
                           on new { r.ProjectId, r.Version } equals new { p.ProjectId, p.Version }
                         group p by r into grp
                         select new { Revision = grp.Key, Participants = grp };

            var filtered = joined.Where(x => x.Participants.Any(p => p.UserId == userId));
            var agg = await filtered.Select(x => new
            {
                x.Revision.Value,
                x.Revision.Margin,
                Weighted = x.Revision.Margin * x.Revision.ProbabilityPercent / 100m
            })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                count = g.Count(),
                valueSum = g.Sum(v => v.Value),
                marginSum = g.Sum(v => v.Margin),
                weightedMarginSum = g.Sum(v => v.Weighted)
            })
            .FirstOrDefaultAsync();

            return Ok(agg ?? new { count = 0, valueSum = 0m, marginSum = 0m, weightedMarginSum = 0m });
        }
        else
        {
            var agg = await baseQ.Select(r => new
            {
                r.Value,
                r.Margin,
                Weighted = r.Margin * r.ProbabilityPercent / 100m
            })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                count = g.Count(),
                valueSum = g.Sum(v => v.Value),
                marginSum = g.Sum(v => v.Margin),
                weightedMarginSum = g.Sum(v => v.Weighted)
            })
            .FirstOrDefaultAsync();

            return Ok(agg ?? new { count = 0, valueSum = 0m, marginSum = 0m, weightedMarginSum = 0m });
        }
    }

    // --- helpers ---
    private static ProjectDto ToDtoWithParticipants(ProjectRevision r, IEnumerable<Guid> participants)
        => new()
        {
            ProjectId = r.ProjectId,
            Version = r.Version,
            EffectiveAt = r.EffectiveAt,
            AMId = r.AMId,
            ClientId = r.ClientId,
            MarketId = r.MarketId,
            Name = r.Name,
            StatusId = r.StatusId,
            Value = r.Value,
            Margin = r.Margin,
            ProbabilityPercent = r.ProbabilityPercent,
            DueQuarter = r.DueQuarter,
            InvoiceMonth = r.InvoiceMonth,
            PaymentQuarter = r.PaymentQuarter,
            VendorId = r.VendorId,
            ArchitectureId = r.ArchitectureId,
            Comment = r.Comment,
            IsCanceled = r.IsCanceled,
            WeightedMargin = Math.Round(r.Margin * r.ProbabilityPercent / 100m, 2),
            Participants = participants.ToList()
        };

    private Guid GetCurrentUserIdOrSystem()
    {
        // TODO: pobierz z JWT/claims; placeholder:
        return Guid.Empty;
    }
}
