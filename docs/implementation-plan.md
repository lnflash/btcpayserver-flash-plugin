# Flash BTCPayServer Plugin Implementation Plan

This document outlines the detailed implementation plan for the Flash BTCPayServer plugin, including phases, tasks, and technical considerations.

## Development Phases

The development is structured in three main phases, each with specific goals and deliverables.

### Phase 1: Core Lightning Integration with Flash

**Duration**: 2-3 weeks
**Objective**: Implement basic Lightning Network integration between BTCPayServer and Flash wallet.

#### Tasks:

1. **Project Setup (Completed)**
   - Create basic project structure
   - Configure dependencies
   - Set up build system

2. **Flash Lightning Client Implementation**
   - Implement `FlashLightningClient.cs` class with all required methods:
     - `GetInvoice`: Fetch invoice details by ID or payment hash
     - `ListInvoices`: List invoices with optional filtering
     - `CreateInvoice`: Create new Lightning invoices
     - `Pay`: Pay Lightning invoices
     - `GetBalance`: Retrieve wallet balance
     - `Listen`: Subscribe to invoice status updates

3. **GraphQL Integration**
   - Map Flash GraphQL API schema to Lightning client methods
   - Implement proper error handling for API responses
   - Create subscription handling for real-time updates

4. **Connection String Handler**
   - Finalize `FlashLightningConnectionStringHandler.cs` implementation
   - Add connection validation and testing
   - Implement wallet ID resolution and default handling

5. **Lightning Setup UI**
   - Complete the Lightning setup tab in BTCPayServer UI
   - Add connection testing functionality
   - Provide helpful setup guidance for users

6. **Testing and Validation**
   - Unit tests for Lightning client functionality
   - Integration tests with Flash API
   - End-to-end payment flow tests

### Phase 2: NFC Card Integration

**Duration**: 3-4 weeks
**Objective**: Implement NFC card functionality for Flash integration.

#### Tasks:

1. **Database Implementation**
   - Finalize data models for card registrations and transactions
   - Implement migrations for schema creation
   - Create database access services

2. **Card Registration System**
   - Implement `FlashCardRegistrationService.cs` functionality:
     - Card registration with Flash wallet mapping
     - Card status management (blocking/unblocking)
     - Transaction logging and history

3. **Card Programming API**
   - Create API endpoints for card programming
   - Implement key derivation for NFC cards
   - Create secure card activation flow

4. **Payment Processing**
   - Implement `FlashPaymentHostedService.cs` for payment event handling
   - Create card tap processing logic
   - Implement pull payment integration

5. **Balance Management**
   - Create balance checking functionality
   - Implement top-up system for cards
   - Add transaction history tracking

6. **Card Security Features**
   - Implement spending limits
   - Add security validations
   - Create card blocking/unblocking functionality

7. **Testing**
   - Unit tests for card registration
   - Integration tests for card programming
   - Security validation tests

### Phase 3: User Interface and Merchant Experience

**Duration**: 2-3 weeks
**Objective**: Create a polished user interface and merchant experience.

#### Tasks:

1. **Card Management UI**
   - Complete card listing view
   - Implement card registration form with NFC scanning
   - Create card details page with history

2. **Merchant Dashboard**
   - Implement merchant-specific views
   - Add analytics and reporting
   - Create card usage statistics

3. **User Experience Enhancements**
   - Add guided setup flow
   - Implement error handling and user feedback
   - Create help documentation

4. **Mobile Responsiveness**
   - Ensure mobile-friendly UI
   - Optimize for touch interfaces
   - Test on various devices

5. **Localization**
   - Add localization support
   - Implement initial translations
   - Create translation guidelines

6. **Performance Optimization**
   - Optimize database queries
   - Implement caching where appropriate
   - Performance testing and tuning

7. **Final Testing and QA**
   - End-to-end testing
   - User acceptance testing
   - Security review

## Technical Implementation Details

### Flash GraphQL API Integration

The integration with Flash GraphQL API requires careful mapping between the `ILightningClient` interface and the Flash API endpoints:

| ILightningClient Method | Flash GraphQL Query/Mutation |
|-------------------------|------------------------------|
| GetInvoice | `InvoiceByPaymentHash` |
| ListInvoices | `Invoices` |
| CreateInvoice | `lnInvoiceCreate` or `lnUsdInvoiceCreate` |
| Pay | `lnInvoicePaymentSend` |
| GetBalance | `GetWallet` |
| Listen | Subscription to `myUpdates` |

See [Flash API Integration](flash-api-integration.md) for detailed mapping.

### NFC Card System Architecture

The NFC card system follows a multi-layered approach:

1. **Card Registration Layer**:
   - Registers card UIDs in the database
   - Maps cards to Pull Payments in BTCPayServer
   - Manages card metadata and status

2. **Card Programming Layer**:
   - Generates keys for NFC cards
   - Creates programming instructions
   - Handles security aspects of card writing

3. **Transaction Processing Layer**:
   - Processes card tap events
   - Validates payments against card balances
   - Updates transaction records

4. **Balance Management Layer**:
   - Tracks card balances through Pull Payments
   - Handles top-ups and refunds
   - Provides balance history

See [NFC Card System](nfc-card-system.md) for detailed architecture.

### Database Schema Design

The database schema includes the following main entities:

1. **CardRegistration**:
   - Primary entity for registered cards
   - Contains card UID, user mapping, and status
   - Links to BTCPayServer Pull Payments

2. **CardTransaction**:
   - Records all card-related transactions
   - Includes payment details and status
   - Supports different transaction types

3. **Relationships**:
   - One-to-many relationship between cards and transactions
   - Links to BTCPayServer entities via foreign keys

See [Database Schema](database-schema.md) for complete schema details.

## Integration with Existing BTCPayServer Components

The plugin integrates with the following BTCPayServer components:

1. **Lightning Network Interface**:
   - Implements `ILightningClient` for Flash integration
   - Registers with `ILightningConnectionStringHandler`

2. **Pull Payment System**:
   - Uses BTCPayServer's Pull Payment for balance management
   - Leverages existing payment flows and security

3. **UI Extensions**:
   - Adds Lightning setup tab
   - Creates navigation menu items
   - Provides dedicated card management pages

4. **Event System**:
   - Subscribes to invoice and payment events
   - Publishes card-related events
   - Integrates with BTCPayServer notification system

## Security Considerations

Security is a critical aspect of the implementation, with focus on:

1. **API Key Management**:
   - Secure handling of API keys
   - Proper authentication for API requests

2. **Card Security**:
   - Secure key derivation for NFC cards
   - Protection against card cloning
   - Spending limits and transaction validation

3. **Payment Security**:
   - Validation of payment requests
   - Prevention of double-spending
   - Transaction integrity checks

4. **Data Protection**:
   - Proper encryption of sensitive data
   - Compliance with data protection regulations
   - Secure storage of card information

See [Security Considerations](security-considerations.md) for detailed security measures.

## Testing Strategy

The testing strategy includes:

1. **Unit Testing**:
   - Individual component testing
   - Mocked dependencies
   - Coverage of edge cases

2. **Integration Testing**:
   - API integration tests
   - Database interaction tests
   - Component interaction tests

3. **End-to-End Testing**:
   - Complete payment flows
   - Card registration and usage
   - Merchant experience testing

4. **Security Testing**:
   - Penetration testing
   - Security review
   - Vulnerability assessment

See [Testing Plan](testing-plan.md) for complete testing strategy.

## Timeline and Milestones

| Milestone | Timeline | Deliverables |
|-----------|----------|--------------|
| Project Setup | Week 1 | Project structure, dependencies, basic components |
| Phase 1 Completion | Week 3-4 | Functional Lightning integration with Flash |
| Phase 2 Completion | Week 7-8 | Working NFC card system and payment processing |
| Phase 3 Completion | Week 10-11 | Polished UI and merchant experience |
| Final Release | Week 12 | Production-ready plugin with documentation |

## Conclusion

This implementation plan provides a structured approach to developing the Flash BTCPayServer plugin, ensuring that all components are properly designed, implemented, and tested. By following this plan, the development team can efficiently create a high-quality plugin that meets the needs of merchants using BTCPayServer with Flash Lightning wallet and NFC card functionality.