using Ganss.Xss;
using Microsoft.AspNetCore.SignalR;
using RedisSample.Server.Api.Infrastructure.Services;
using RedisSample.Server.Api.Infrastructure.SignalR;
using RedisSample.Server.Infrastructure.Redis.Caching;
using RedisSample.Shared.Features.Products;

namespace RedisSample.Server.Api.Features.Products;

[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AuthPolicies.PRIVILEGED_ACCESS),
    Authorize(Policy = AppFeatures.AdminPanel.ProductCatalog_Manage)]
public partial class ProductController : AppControllerBase, IProductController
{
    [AutoInject] private HtmlSanitizer htmlSanitizer = default!;

    [AutoInject] private IHubContext<AppHub> appHubContext = default!;
    [AutoInject] private ProductEmbeddingService productEmbeddingService = default!;
    [AutoInject] private ResponseCacheService responseCacheService = default!;
    [AutoInject] private IRedisService _redis = default!;

    [HttpGet, EnableQuery]
    public IQueryable<ProductDto> Get()
    {
        return DbContext.Products
            .Project();
    }

    [HttpGet]
    public async Task<PagedResponse<ProductDto>> GetProducts(ODataQueryOptions<ProductDto> odataQuery, CancellationToken cancellationToken)
    {
        var query = (IQueryable<ProductDto>)odataQuery.ApplyTo(Get(), ignoreQueryOptions: AllowedQueryOptions.Top | AllowedQueryOptions.Skip);

        var totalCount = await query.LongCountAsync(cancellationToken);

        query = query.SkipIf(odataQuery.Skip is not null, odataQuery.Skip?.Value)
                     .TakeIf(odataQuery.Top is not null, odataQuery.Top?.Value);

        return new PagedResponse<ProductDto>(await query.ToArrayAsync(cancellationToken), totalCount);
    }

    [HttpGet("{searchQuery}")]
    public async Task<PagedResponse<ProductDto>> SearchProducts(string searchQuery, ODataQueryOptions<ProductDto> odataQuery, CancellationToken cancellationToken)
    {
        var query = (IQueryable<ProductDto>)odataQuery.ApplyTo((await (productEmbeddingService.SearchProducts(searchQuery, cancellationToken))).Project(),
            ignoreQueryOptions: AllowedQueryOptions.Top | AllowedQueryOptions.Skip | AllowedQueryOptions.OrderBy /* Ordering can disrupt the results of the embedding service. */);
        var totalCount = await query.LongCountAsync(cancellationToken);

        query = query.SkipIf(odataQuery.Skip is not null, odataQuery.Skip?.Value)
                     .TakeIf(odataQuery.Top is not null, odataQuery.Top?.Value);

        return new PagedResponse<ProductDto>(await query.ToArrayAsync(cancellationToken), totalCount);
    }

    [HttpGet("{id}")]
    public async Task<ProductDto> Get(Guid id, CancellationToken cancellationToken)
    {
        return await getProductFromRedis(id);
        var dto = await Get().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ProductCouldNotBeFound)]);

        return dto;
    }
    private void setProductToRedis(ProductDto dto)
    {
        var key = $"product:{dto.Id}";
        _redis.SetAsync(key, dto, TimeSpan.FromHours(1));

    }

    private ProductDto getProductToRedis(ProductDto dto)
    {
        var key = $"product:{dto.Id}";
        return _redis.GetAsync<ProductDto>(key).Result;
    }
    private async Task RemoveProductFromRedis(Guid id)
    {
        var key = $"product:{id}";
        await _redis.RemoveAsync(key);
    }
    private async Task<bool> ExistsProductInRedis(Guid id)
    {
        var key = $"product:{id}";
        return await _redis.ExistsAsync(key);
    }
    private async Task<ProductDto> getProductFromRedis(Guid id)
    {
        var key = $"product:{id}";
        var product = await _redis.GetAsync<ProductDto>(key);
        if (product == null)
        {
            var dto = await Get().FirstOrDefaultAsync(t => t.Id == id)
                ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ProductCouldNotBeFound)]);
            setProductToRedis(dto);
            return dto;
        }
        return product;
    }


    [HttpPost]
    public async Task<ProductDto> Create(ProductDto dto, CancellationToken cancellationToken)
    {
        dto.DescriptionHTML = htmlSanitizer.Sanitize(dto.DescriptionHTML ?? string.Empty);

        var entityToAdd = dto.Map();

        entityToAdd.CreatedOn = TimeProvider.GetUtcNow();

        await DbContext.Products.AddAsync(entityToAdd, cancellationToken);

        await Validate(entityToAdd, cancellationToken);

            await productEmbeddingService.Embed(entityToAdd, cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await PublishDashboardDataChanged(cancellationToken);

        var _dtoEntity= entityToAdd.Map();
        setProductToRedis(_dtoEntity);
        return _dtoEntity;
    }

    [HttpPut]
    public async Task<ProductDto> Update(ProductDto dto, CancellationToken cancellationToken)
    {
        dto.DescriptionHTML = htmlSanitizer.Sanitize(dto.DescriptionHTML ?? string.Empty);

        var entityToUpdate = await DbContext.Products.FindAsync([dto.Id], cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ProductCouldNotBeFound)]);

        dto.Patch(entityToUpdate);

        await Validate(entityToUpdate, cancellationToken);

            await productEmbeddingService.Embed(entityToUpdate, cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await responseCacheService.PurgeProductCache(entityToUpdate.ShortId);

        await PublishDashboardDataChanged(cancellationToken);

        var _dtoEntity= entityToUpdate.Map();
        setProductToRedis(_dtoEntity);
        return _dtoEntity;
    }

    [HttpDelete("{id}/{version}")]
    public async Task Delete(Guid id, long version, CancellationToken cancellationToken)
    {
        var key = $"product:{id}";
        var _ifREdis=ExistsProductInRedis(id);
        var entityToDelete = await DbContext.Products.FindAsync([id], cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ProductCouldNotBeFound)]);

        entityToDelete.Version = version;

        DbContext.Remove(entityToDelete);

        await DbContext.SaveChangesAsync(cancellationToken);

        await responseCacheService.PurgeProductCache(entityToDelete.ShortId);

        await PublishDashboardDataChanged(cancellationToken);
        if (await ExistsProductInRedis(id))
        {
            await RemoveProductFromRedis(id);
        }
    }

    private async Task PublishDashboardDataChanged(CancellationToken cancellationToken)
    {
        // Check out AppHub's comments for more info.
        // In order to exclude current user session, gets its signalR connection id from database and use GroupExcept instead.
        await appHubContext.Clients.Group("AuthenticatedClients").Publish(SharedAppMessages.DASHBOARD_DATA_CHANGED, null, cancellationToken);
    }

    private async Task Validate(Product product, CancellationToken cancellationToken)
    {
        var entry = DbContext.Entry(product);
        // Remote validation example: Any errors thrown here will be displayed in the client's edit form component.
        if ((entry.State is EntityState.Added || entry.Property(c => c.Name).IsModified)
            && await DbContext.Products.AnyAsync(p => p.Name == product.Name, cancellationToken))
            throw new ResourceValidationException((nameof(ProductDto.Name), [Localizer[nameof(AppStrings.DuplicateProductName)]]));
    }
}

