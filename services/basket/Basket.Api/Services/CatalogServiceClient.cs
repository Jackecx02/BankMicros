using System.Net;
using System.Net.Http.Json;

namespace Basket.Api.Services;

public class CatalogServiceClient(HttpClient httpClient) : ICatalogServiceClient
{
    public async Task<CatalogProductDto?> GetProductAsync(Guid productId)
    {
        var response = await httpClient.GetAsync($"/products/{productId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CatalogProductDto>();
    }
}
