# Development

## Building from Source

### Prerequisites
- .NET 8 SDK
- Git
- Docker (optional, for local BTCPayServer)

### Clone and Build

```bash
git clone https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash.git
cd BTCPayServer.Plugins.Flash
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Development Setup

### Local BTCPayServer

1. Clone BTCPayServer:
```bash
git clone https://github.com/btcpayserver/btcpayserver.git
cd btcpayserver
```

2. Run with Docker:
```bash
docker-compose up
```

3. Place plugin in `BTCPayServer.Plugins/` directory

4. Run with plugin:
```bash
dotnet run --launch-profile Docker
```

## Build Scripts

### Build Package
```bash
./build-package.sh
```
Creates a `.btcpay` package file for distribution.

### Direct Install (Docker)
```bash
./direct-install.sh
```
Installs plugin directly to running BTCPay container.

### Run Tests
```bash
./run-tests.sh
```
Executes full test suite.

## Version Management

When updating the plugin version, update ALL THREE locations:

1. **FlashPlugin.cs**:
```csharp
public override Version Version => new Version(x, y, z);
```

2. **manifest.json**:
```json
"version": "x.y.z"
```

3. **BTCPayServer.Plugins.Flash.csproj**:
```xml
<Version>x.y.z</Version>
```

## Architecture

### Service Layer
- **GraphQLService**: Core GraphQL communication
- **InvoiceService**: Invoice management
- **PaymentService**: Payment processing
- **ExchangeRateService**: Currency conversion
- **BoltcardService**: NFC card handling
- **WebSocketService**: Real-time updates
- **MonitoringService**: Health checks
- **WalletService**: Wallet operations

### Data Layer
- Entity Framework Core with PostgreSQL
- Migrations in `Data/Migrations/`
- Repository pattern for data access

### Error Handling
- Polly retry policies for resilience
- Custom exceptions in `Exceptions/`
- Comprehensive logging throughout

## Testing

### Unit Tests
Test individual services and components:
```bash
dotnet test --filter Category=Unit
```

### Integration Tests
Test with real Flash API (requires credentials):
```bash
dotnet test --filter Category=Integration
```

### Test Coverage
Generate coverage report:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Code Style

### C# Conventions
- Use async/await for all I/O operations
- Follow Microsoft C# naming conventions
- Add XML documentation to public methods
- Use dependency injection

### Best Practices
- Never hardcode secrets or API keys
- Use `ILogger` for all logging
- Handle all exceptions appropriately
- Write unit tests for new features
- Update version in all three locations

## Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/my-feature`
3. Make changes and test thoroughly
4. Commit with clear message: `git commit -m "feat: Add new feature"`
5. Push branch: `git push origin feature/my-feature`
6. Create Pull Request with description

### Commit Message Format
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `refactor:` Code refactoring
- `test:` Test additions/changes
- `chore:` Maintenance tasks

## Release Process

1. Update version in all three locations
2. Update CHANGELOG.md
3. Run full test suite
4. Build package: `./build-package.sh`
5. Create GitHub release
6. Upload `.btcpay` file to release
7. Submit to BTCPayServer plugin directory

## Debugging

### Visual Studio / VS Code
1. Set `BTCPayServer.Plugins.Flash` as startup project
2. Configure launch settings for Docker profile
3. Set breakpoints in code
4. Press F5 to debug

### Remote Debugging
1. Enable debug logging in production
2. Access logs via Docker or file system
3. Use Application Insights for telemetry (if configured)

## Resources

- [BTCPayServer Plugin Documentation](https://docs.btcpayserver.org/Development/Plugins/)
- [Flash API Documentation](https://api.flashapp.me/graphql)
- [Lightning Network Specifications](https://github.com/lightning/bolts)
- [LNURL Specifications](https://github.com/lnurl/luds)