using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TreinAI.Api.Avaliacoes.Validators;
using TreinAI.Shared.Exceptions;
using TreinAI.Shared.Middleware;
using TreinAI.Shared.Models;
using TreinAI.Shared.Repositories;
using TreinAI.Shared.Validation;

namespace TreinAI.Api.Avaliacoes.Functions;

/// <summary>
/// CRUD for Avaliacao (physical assessment).
/// </summary>
public class AvaliacaoFunctions
{
    private readonly IRepository<Avaliacao> _repository;
    private readonly TenantContext _tenantContext;
    private readonly ILogger<AvaliacaoFunctions> _logger;

    public AvaliacaoFunctions(
        IRepository<Avaliacao> repository,
        TenantContext tenantContext,
        ILogger<AvaliacaoFunctions> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [Function("GetAvaliacoes")]
    public async Task<HttpResponseData> GetAvaliacoes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "avaliacoes")] HttpRequestData req)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var alunoId = queryParams["alunoId"];

        IReadOnlyList<Avaliacao> avaliacoes;

        if (!string.IsNullOrEmpty(alunoId))
        {
            avaliacoes = await _repository.QueryAsync(
                _tenantContext.TenantId, a => a.AlunoId == alunoId);
        }
        else if (_tenantContext.IsAluno)
        {
            avaliacoes = await _repository.QueryAsync(
                _tenantContext.TenantId, a => a.AlunoId == _tenantContext.UserId);
        }
        else if (_tenantContext.IsProfessor)
        {
            avaliacoes = await _repository.QueryAsync(
                _tenantContext.TenantId, a => a.ProfessorId == _tenantContext.UserId);
        }
        else
        {
            avaliacoes = await _repository.GetAllAsync(_tenantContext.TenantId);
        }

        return await ValidationHelper.OkAsync(req, avaliacoes);
    }

    [Function("GetAvaliacaoById")]
    public async Task<HttpResponseData> GetAvaliacaoById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "avaliacoes/{id}")] HttpRequestData req,
        string id)
    {
        var avaliacao = await _repository.GetByIdAsync(id, _tenantContext.TenantId);
        if (avaliacao == null)
            throw new NotFoundException("Avaliacao", id);

        if (_tenantContext.IsAluno && avaliacao.AlunoId != _tenantContext.UserId)
            throw new ForbiddenException("Você só pode acessar suas próprias avaliações.");

        return await ValidationHelper.OkAsync(req, avaliacao);
    }

    [Function("CreateAvaliacao")]
    public async Task<HttpResponseData> CreateAvaliacao(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "avaliacoes")] HttpRequestData req)
    {
        if (_tenantContext.IsAluno)
            throw new ForbiddenException("Apenas professores podem criar avaliações.");

        var validator = new AvaliacaoValidator();
        var avaliacao = await ValidationHelper.ValidateRequestAsync(req, validator);

        avaliacao.TenantId = _tenantContext.TenantId;
        avaliacao.ProfessorId = _tenantContext.UserId;
        avaliacao.CreatedBy = _tenantContext.UserId;
        avaliacao.UpdatedBy = _tenantContext.UserId;

        // Auto-calculate IMC
        if (avaliacao.Peso.HasValue && avaliacao.Altura.HasValue && avaliacao.Altura.Value > 0)
        {
            var alturaM = avaliacao.Altura.Value / 100.0;
            avaliacao.Imc = Math.Round(avaliacao.Peso.Value / (alturaM * alturaM), 2);
        }

        var created = await _repository.CreateAsync(avaliacao);
        return await ValidationHelper.CreatedAsync(req, created);
    }

    [Function("UpdateAvaliacao")]
    public async Task<HttpResponseData> UpdateAvaliacao(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "avaliacoes/{id}")] HttpRequestData req,
        string id)
    {
        if (_tenantContext.IsAluno)
            throw new ForbiddenException("Apenas professores podem editar avaliações.");

        var existing = await _repository.GetByIdAsync(id, _tenantContext.TenantId);
        if (existing == null)
            throw new NotFoundException("Avaliacao", id);

        if (_tenantContext.IsProfessor && existing.ProfessorId != _tenantContext.UserId)
            throw new ForbiddenException("Você só pode editar avaliações que criou.");

        var validator = new AvaliacaoValidator();
        var avaliacao = await ValidationHelper.ValidateRequestAsync(req, validator);

        avaliacao.Id = id;
        avaliacao.TenantId = _tenantContext.TenantId;
        avaliacao.ProfessorId = existing.ProfessorId;
        avaliacao.CreatedAt = existing.CreatedAt;
        avaliacao.CreatedBy = existing.CreatedBy;
        avaliacao.UpdatedBy = _tenantContext.UserId;

        if (avaliacao.Peso.HasValue && avaliacao.Altura.HasValue && avaliacao.Altura.Value > 0)
        {
            var alturaM = avaliacao.Altura.Value / 100.0;
            avaliacao.Imc = Math.Round(avaliacao.Peso.Value / (alturaM * alturaM), 2);
        }

        var updated = await _repository.UpdateAsync(avaliacao);
        return await ValidationHelper.OkAsync(req, updated);
    }

    [Function("DeleteAvaliacao")]
    public async Task<HttpResponseData> DeleteAvaliacao(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "avaliacoes/{id}")] HttpRequestData req,
        string id)
    {
        if (_tenantContext.IsAluno)
            throw new ForbiddenException("Apenas professores podem excluir avaliações.");

        var existing = await _repository.GetByIdAsync(id, _tenantContext.TenantId);
        if (existing == null)
            throw new NotFoundException("Avaliacao", id);

        await _repository.DeleteAsync(id, _tenantContext.TenantId);
        return ValidationHelper.NoContent(req);
    }

    /// <summary>
    /// GET /api/alunos/{alunoId}/avaliacoes — Get all assessments for a student.
    /// </summary>
    [Function("GetAvaliacoesByAluno")]
    public async Task<HttpResponseData> GetAvaliacoesByAluno(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "alunos/{alunoId}/avaliacoes")] HttpRequestData req,
        string alunoId)
    {
        if (_tenantContext.IsAluno && alunoId != _tenantContext.UserId)
            throw new ForbiddenException("Você só pode acessar suas próprias avaliações.");

        var avaliacoes = await _repository.QueryAsync(
            _tenantContext.TenantId, a => a.AlunoId == alunoId);

        return await ValidationHelper.OkAsync(req, avaliacoes);
    }
}
