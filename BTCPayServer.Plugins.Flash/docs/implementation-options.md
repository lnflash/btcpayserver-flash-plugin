# Flash BTCPayServer Plugin: Authentication Implementation Options

This document outlines two approaches for authenticating the BTCPayServer Flash plugin with the Flash backend:

1. **JWT Authentication** - Using existing authentication mechanisms
2. **API Key System** - Implementing a new API key infrastructure

## Option 1: JWT Authentication (Short-term Solution)

### Overview
Leverage Flash's existing JWT-based authentication system to authenticate BTCPayServer with the Flash backend.

### Implementation Details

#### Connection String Format
```
type=flash;server=https://api.flashapp.me/graphql;email=user@example.com;password=securepassword;wallet-id=optional-wallet-id
```

#### Authentication Flow
1. Store email/password in BTCPayServer's encrypted connection string storage
2. Authenticate to obtain JWT token when starting the plugin or when token expires
3. Use JWT token for all GraphQL API requests
4. Implement token refresh logic to handle expirations

#### Code Sample - Authentication
```csharp
private async Task<string> GetAuthTokenAsync()
{
    // Check if we have a valid token already
    if (!string.IsNullOrEmpty(_authToken) && _tokenExpiry > DateTime.UtcNow)
        return _authToken;
        
    // Otherwise authenticate to get a new token
    var loginMutation = new GraphQLRequest
    {
        Query = @"
        mutation Login($email: String!, $password: String!) {
          login(email: $email, password: $password) {
            token
            expiresAt
          }
        }",
        Variables = new
        {
            email = _email,
            password = _password
        }
    };
    
    var response = await _client.SendMutationAsync<JObject>(loginMutation);
    _authToken = response.Data["login"]["token"].ToString();
    var expiryTimestamp = response.Data["login"]["expiresAt"].Value<long>();
    _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp).DateTime;
    
    return _authToken;
}
```

#### Advantages
- **Immediate Implementation**: Can be implemented without backend changes
- **Simplicity**: Uses existing authentication flow
- **Compatibility**: Works with current Flash backend

#### Drawbacks
See [security-analysis-jwt-vs-apikeys.md](security-analysis-jwt-vs-apikeys.md) for detailed security and architectural concerns.

## Option 2: API Key System (Long-term Solution)

### Overview
Implement a dedicated API key system in the Flash backend for machine-to-machine authentication.

### Implementation Details

#### Backend Changes Required
1. Create `ApiKey` database model and migrations
2. Implement API key generation, validation, and management services
3. Add GraphQL mutations and queries for API key operations
4. Create middleware for API key authentication
5. Add user interface for managing API keys

#### Connection String Format
```
type=flash;server=https://api.flashapp.me/graphql;api-key=your-api-key;wallet-id=optional-wallet-id
```

#### Authentication Flow
1. Store API key in BTCPayServer's encrypted connection string storage
2. Include API key in request headers (e.g., `X-API-KEY: your-api-key`)
3. Flash backend validates the API key and authorizes the request

#### Code Sample - API Key Usage
```csharp
public FlashLightningClient(string apiKey, Uri apiEndpoint, string walletId, Network network, HttpClient httpClient, ILogger logger)
{
    _apiKey = apiKey;
    _apiEndpoint = apiEndpoint;
    WalletId = walletId;
    _network = network;
    Logger = logger;
    
    // Configure GraphQL client with API key authentication
    _client = new GraphQLHttpClient(new GraphQLHttpClientOptions
    {
        EndPoint = _apiEndpoint,
        // ...other options...
    }, new NewtonsoftJsonSerializer(), httpClient);
    
    // Add API key to headers
    _client.HttpClient.DefaultRequestHeaders.Add("X-API-KEY", _apiKey);
}
```

#### Advantages
- **Security**: Follows security best practices for service-to-service authentication
- **Granular Control**: Can implement scoped permissions and limits
- **Management**: Easy to rotate, revoke, and monitor usage
- **Separation of Concerns**: Properly separates human and machine authentication

#### Drawbacks
- **Development Effort**: Requires backend changes
- **Timeline**: Takes longer to implement
- **Testing**: Requires additional testing of new authentication flow

## Recommendation

1. **Short-term**: Implement JWT-based authentication as a temporary solution to enable immediate integration
2. **Long-term**: Develop the API key system and migrate to it when ready

To manage the security risks of the short-term solution:
- Create dedicated user accounts for BTCPayServer integration with minimal permissions
- Store credentials securely using BTCPayServer's encryption capabilities
- Implement proper token refresh logic to maintain connection
- Monitor integration account activity closely

Regardless of which solution is implemented, thoroughly document the authentication flow, security considerations, and usage instructions for plugin users.