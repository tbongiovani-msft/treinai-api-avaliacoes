using FluentValidation;
using TreinAI.Shared.Models;

namespace TreinAI.Api.Avaliacoes.Validators;

public class AvaliacaoValidator : AbstractValidator<Avaliacao>
{
    public AvaliacaoValidator()
    {
        RuleFor(x => x.AlunoId)
            .NotEmpty().WithMessage("AlunoId é obrigatório.");

        RuleFor(x => x.DataAvaliacao)
            .NotEmpty().WithMessage("Data da avaliação é obrigatória.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1)).WithMessage("Data da avaliação não pode ser no futuro.");

        RuleFor(x => x.Peso)
            .GreaterThan(0).WithMessage("Peso deve ser maior que zero.")
            .When(x => x.Peso.HasValue);

        RuleFor(x => x.Altura)
            .GreaterThan(0).WithMessage("Altura deve ser maior que zero.")
            .When(x => x.Altura.HasValue);

        RuleFor(x => x.PercentualGordura)
            .InclusiveBetween(0, 100).WithMessage("Percentual de gordura deve estar entre 0 e 100.")
            .When(x => x.PercentualGordura.HasValue);
    }
}
