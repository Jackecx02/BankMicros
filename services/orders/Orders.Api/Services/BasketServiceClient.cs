using System.Net;
using System.Net.Http.Json;

namespace Orders.Api.Services;

public class BasketServiceClient(HttpClient httpClient) : IBasketServiceClient
{
    public async Task<BasketDto?> GetBasketAsync(Guid userId)
    {
        var response = await httpClient.GetAsync($"/basket/{userId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BasketDto>();
    }
}
