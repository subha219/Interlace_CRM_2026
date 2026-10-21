using Application.Common.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SeedManager.Demos;

public class SupportCaseCommentSeeder
{
    private readonly ICommandRepository<SupportCaseComment> _commentRepository;
    private readonly ICommandRepository<SupportCase> _caseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SupportCaseCommentSeeder(
        ICommandRepository<SupportCaseComment> commentRepository,
        ICommandRepository<SupportCase> caseRepository,
        IUnitOfWork unitOfWork
    )
    {
        _commentRepository = commentRepository;
        _caseRepository = caseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task GenerateDataAsync()
    {
        var random = new Random();

        var caseIds = await _caseRepository.GetQuery()
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync();

        var comments = new[]
        {
            "Acknowledged the case and started investigating.",
            "Asked the customer for screenshots and the exact steps to reproduce.",
            "Escalated to the technical team for review.",
            "Shared a workaround with the customer while the fix is prepared.",
            "Customer replied and confirmed the issue is no longer happening."
        };

        foreach (var caseId in caseIds)
        {
            var count = random.Next(0, 4);
            for (var i = 0; i < count; i++)
            {
                await _commentRepository.CreateAsync(new SupportCaseComment
                {
                    SupportCaseId = caseId,
                    Comment = comments[random.Next(comments.Length)]
                });
            }
        }

        await _unitOfWork.SaveAsync();
    }
}
