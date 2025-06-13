# BTCPayServer Flash Plugin - Development Roadmap

## Project Status
- **Current Version**: 1.3.6-dev
- **Last Stable Release**: 1.3.5
- **Development Phase**: Service Architecture Implemented ✅ 

## Recent Achievements (Completed Today)

### ✅ Service Architecture Foundation (Phase 1)
- **Status**: COMPLETED - Clean separation of concerns achieved
- **Completed**:
  - Created 5 service interfaces with comprehensive documentation
  - Implemented all core services with dependency injection
  - Successfully refactored main client to delegate to services
  - Maintained full backward compatibility
  - Zero compilation errors, successful build

### ✅ Phase 2A: Complete Service Extraction
- **Status**: COMPLETED - Core functionality delegated to services
- **Completed**:
  - Invoice operations → `FlashInvoiceService`
  - Payment operations → `FlashPaymentService`
  - GraphQL operations → `FlashGraphQLService`
  - Exchange rate operations → `FlashExchangeRateService`
  - Boltcard operations → `FlashBoltcardService`
  - Updated invoice listener to use service architecture
  - Main client now acts as a thin orchestration layer

## Priority Matrix

### 🚨 CRITICAL (Address Immediately)

#### 1. Invoice Status Notification System
- **Status**: ✅ COMPLETED - Notification bridge working correctly
- **Impact**: HIGH - Users see "unpaid" invoices when payments succeeded
- **Effort**: HIGH 
- **Location**: Now handled via `FlashInvoiceService.MarkInvoiceAsPaidAsync()`
- **Issue**: Payment notifications not properly propagating to BTCPay Server core
- **Notes**: ✅ RESOLVED: BTCPay Server notification bridge implemented and functional

#### 2. Security: Remove Hardcoded Development Tokens
- **Status**: ✅ COMPLETED
- **Impact**: CRITICAL - Security vulnerability in production
- **Effort**: LOW
- **Fixed**: All hardcoded tokens removed from codebase
- **Solution**: Enforced proper dependency injection, components now require configured clients

### 🟡 HIGH PRIORITY (Next Sprint)

#### 3. Complete Service Implementation (Phase 2B)
- **Status**: 🔄 READY TO START
- **Impact**: HIGH - Code quality and maintainability
- **Effort**: MEDIUM
- **Next Steps**:
  - Move remaining helper methods to appropriate services
  - Extract transaction monitoring logic to dedicated service
  - Implement proper error handling patterns
  - Add comprehensive unit tests for each service

#### 4. Enhanced Error Handling
- **Status**: 📋 PLANNED
- **Impact**: HIGH - User experience
- **Effort**: MEDIUM
- **Issues**:
  - Generic error messages don't help users troubleshoot
  - Flash API errors need better translation
  - Network timeouts not handled gracefully
- **Solution**: Implement error handling service with user-friendly messages

### 🟢 MEDIUM PRIORITY (Future Releases)

#### 5. Performance Optimization
- **Status**: 📋 PLANNED
- **Impact**: MEDIUM - System efficiency
- **Effort**: MEDIUM
- **Areas**:
  - GraphQL query optimization (batch operations)
  - Implement proper caching strategy
  - Reduce API calls for exchange rates
  - Optimize invoice polling mechanism

#### 6. Enhanced Monitoring & Diagnostics
- **Status**: 📋 PLANNED
- **Impact**: MEDIUM - Operations support
- **Effort**: LOW
- **Features**:
  - Health check endpoints
  - Performance metrics
  - API call tracking
  - Error rate monitoring

#### 7. Configuration UI Enhancement
- **Status**: 📋 PLANNED  
- **Impact**: MEDIUM - User onboarding
- **Effort**: MEDIUM
- **Features**:
  - Visual configuration wizard
  - Connection testing tools
  - Wallet selection interface
  - Settings validation

### 🔵 LOW PRIORITY (Nice to Have)

#### 8. Multi-Wallet Support
- **Status**: 📋 FUTURE
- **Impact**: LOW - Advanced feature
- **Effort**: HIGH
- **Description**: Allow switching between multiple Flash wallets

#### 9. Transaction Export Features
- **Status**: 📋 FUTURE
- **Impact**: LOW - Reporting
- **Effort**: LOW
- **Description**: CSV/JSON export of Flash transactions

#### 10. WebSocket Support
- **Status**: ✅ COMPLETED
- **Impact**: HIGH - Real-time updates dramatically improve user experience
- **Effort**: HIGH
- **Implemented**: Full WebSocket service with automatic reconnection and fallback to polling
- **Benefits**: Instant invoice status updates, reduced server load, better performance

## Architecture Evolution

### Current State (v1.3.6-dev)
```
FlashLightningClient (Orchestrator)
    ├── IFlashGraphQLService (API Communication)
    ├── IFlashInvoiceService (Invoice Management)
    ├── IFlashPaymentService (Payment Processing)
    ├── IFlashBoltcardService (Boltcard Features)
    └── IFlashExchangeRateService (Currency Conversion)
```

### Target State (v1.4.0)
```
FlashLightningClient (Thin Interface Layer)
    ├── Core Services
    │   ├── IFlashGraphQLService
    │   ├── IFlashInvoiceService
    │   ├── IFlashPaymentService
    │   └── IFlashExchangeRateService
    ├── Feature Services
    │   ├── IFlashBoltcardService
    │   ├── IFlashMonitoringService
    │   └── IFlashNotificationService
    └── Support Services
        ├── IFlashConfigurationService
        ├── IFlashErrorHandlingService
        └── IFlashCachingService
```

## Development Guidelines

### Code Quality Standards
- ✅ All new code must use service interfaces
- ✅ Dependency injection for all services
- ✅ Comprehensive logging for debugging
- ✅ Thread-safe implementations required
- 📋 Unit tests for all public methods
- 📋 Integration tests for critical paths

### Testing Requirements
- Unit test coverage target: 80%
- Integration tests for all API operations
- Manual testing checklist for releases
- Performance benchmarks for key operations

### Release Process
1. Feature freeze 1 week before release
2. Full regression testing
3. Performance validation
4. Security review
5. Documentation update
6. Version bump and tagging

## Version History
- **v1.3.5**: Stable release with LNURL support
- **v1.3.6-dev**: Service architecture refactoring
- **v1.4.0** (planned): Complete service implementation
- **v1.5.0** (planned): Enhanced monitoring and diagnostics
- **v2.0.0** (future): Multi-wallet support with UI overhaul