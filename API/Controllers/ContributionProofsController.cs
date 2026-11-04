using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Controllers;

[Authorize(Policy = Policies.SignedIn)]
[ApiController]
[Route(ApiRoutes.V1 + "/contributions/{contributionId:guid}/proof")]
public sealed class ContributionProofsController(
    RondiTrackDbContext db,
    StokvelAuthorization authorization) : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ContributionProofResponse>> Upload(
        Guid contributionId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var contribution = await db.Contributions
            .Include(x => x.Cycle)
            .Include(x => x.Proof)
            .SingleOrDefaultAsync(x => x.Id == contributionId, cancellationToken)
            ?? throw new NotFoundException("Proof not found.");
        await authorization.EnsureAsync(User, contribution.StokvelId, Policies.StokvelView);
        var callerId = StokvelAuthorization.UserId(User);
        if (callerId != contribution.UserId && !await IsTreasurerAsync(contribution.StokvelId, callerId, cancellationToken))
            throw new NotFoundException("Proof not found.");
        if (contribution.Cycle.Status != "Open")
            throw new BusinessRuleException("Proof is locked after the cycle closes.");
        if (file is null || file.Length == 0)
            throw new RequestValidationException("A proof file is required.");
        if (file.Length > MaxBytes)
            throw new RequestValidationException("Proof files must be 5 MB or smaller.");

        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var detected = Detect(bytes);
        if (detected is null)
            throw new RequestValidationException("Upload a valid PDF, JPEG or PNG file.");

        if (contribution.Proof is not null)
            db.ContributionProofs.Remove(contribution.Proof);
        var proof = new ContributionProof(contribution.Id, file.FileName, detected.Value.ContentType, bytes);
        db.ContributionProofs.Add(proof);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(proof));
    }

    [HttpGet]
    public async Task<ActionResult<ContributionProofResponse>> Metadata(Guid contributionId, CancellationToken cancellationToken)
    {
        var proof = await GetAuthorizedProofAsync(contributionId, cancellationToken);
        return Ok(ToResponse(proof));
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(Guid contributionId, CancellationToken cancellationToken)
    {
        var proof = await GetAuthorizedProofAsync(contributionId, cancellationToken);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        return File(proof.Contents, proof.ContentType, proof.FileName, enableRangeProcessing: false);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(Guid contributionId, CancellationToken cancellationToken)
    {
        var proof = await GetAuthorizedProofAsync(contributionId, cancellationToken);
        var contribution = proof.Contribution;
        if (contribution.Cycle.Status != "Open")
            throw new BusinessRuleException("Proof is locked after the cycle closes.");
        db.ContributionProofs.Remove(proof);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ContributionProof> GetAuthorizedProofAsync(Guid contributionId, CancellationToken cancellationToken)
    {
        var contribution = await db.Contributions
            .Include(x => x.Proof)
            .Include(x => x.Cycle)
            .SingleOrDefaultAsync(x => x.Id == contributionId, cancellationToken);
        if (contribution?.Proof is null)
            throw new NotFoundException("Proof not found.");
        var callerId = StokvelAuthorization.UserId(User);
        var allowed = callerId == contribution.UserId ||
            await IsTreasurerAsync(contribution.StokvelId, callerId, cancellationToken);
        if (!allowed)
            throw new NotFoundException("Proof not found.");
        return contribution.Proof;
    }

    private Task<bool> IsTreasurerAsync(Guid stokvelId, Guid userId, CancellationToken cancellationToken) =>
        db.StokvelMembers.AnyAsync(x => x.StokvelId == stokvelId && x.UserId == userId &&
            (x.Role == Roles.Treasurer || x.Role == Roles.Chairperson), cancellationToken);

    private static ContributionProofResponse ToResponse(ContributionProof proof) =>
        new(proof.Id, proof.ContributionId, proof.FileName, proof.ContentType, proof.Length, proof.UploadedAtUtc);

    private static (string ContentType, string Extension)? Detect(byte[] bytes)
    {
        if (bytes.Length >= Pdf.Length && bytes.AsSpan(0, Pdf.Length).SequenceEqual(Pdf))
            return ("application/pdf", ".pdf");
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return ("image/jpeg", ".jpg");
        if (bytes.Length >= 8 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
            bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10)
            return ("image/png", ".png");
        return null;
    }
}
