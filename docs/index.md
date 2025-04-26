# Flash BTCPayServer Plugin Documentation

## Documentation Index

1. [Overview](README.md)
2. [Architecture](architecture.md) - System architecture and component interactions
3. [Implementation Plan](implementation-plan.md) - Detailed development phases and tasks
4. [Flash API Integration](flash-api-integration.md) - Integration with Flash GraphQL API
5. [NFC Card System](nfc-card-system.md) - NFC card functionality implementation
6. [Database Schema](database-schema.md) - Database models and relationships
7. [API Reference](api-reference.md) - Plugin API endpoints documentation
8. [UI Components](ui-components.md) - User interface components and flows

## Quick Links

- [Project Setup](implementation-plan.md#phase-1-core-lightning-integration-with-flash)
- [Lightning Client Implementation](flash-api-integration.md#api-mapping)
- [Card Registration](nfc-card-system.md#card-registration-system)
- [Payment Processing](nfc-card-system.md#payment-processing-system)
- [API Endpoints](api-reference.md#api-endpoints)
- [UI Components](ui-components.md#ui-component-map)

## Development Roadmap

### Phase 1: Core Lightning Integration

**Duration**: 2-3 weeks
**Objective**: Implement basic Lightning Network integration between BTCPayServer and Flash wallet.

Key tasks:
- Complete Lightning client implementation
- Implement GraphQL integration
- Finalize connection string handler
- Create Lightning setup UI

[Full Phase 1 Details](implementation-plan.md#phase-1-core-lightning-integration-with-flash)

### Phase 2: NFC Card Integration

**Duration**: 3-4 weeks
**Objective**: Implement NFC card functionality for Flash integration.

Key tasks:
- Implement database models and migrations
- Create card registration system
- Implement card programming
- Develop payment processing

[Full Phase 2 Details](implementation-plan.md#phase-2-nfc-card-integration)

### Phase 3: User Interface and Merchant Experience

**Duration**: 2-3 weeks
**Objective**: Create a polished user interface and merchant experience.

Key tasks:
- Complete card management UI
- Implement merchant dashboard
- Add UX enhancements
- Optimize performance

[Full Phase 3 Details](implementation-plan.md#phase-3-user-interface-and-merchant-experience)

## Contributing

We welcome contributions to the Flash BTCPayServer plugin. Please see the [Contributing Guide](contributing.md) for details on how to contribute.

## License

This project is licensed under the MIT License - see the LICENSE file in the root directory for details.