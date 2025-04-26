# UI Components

This document describes the user interface components and flows provided by the Flash BTCPayServer plugin.

## Overview

The Flash BTCPayServer plugin provides a set of user interface components for:

1. **Lightning Integration**: Configuration and management of Flash Lightning connection
2. **Card Management**: Registration, programming, and management of Flash cards
3. **Transaction History**: Viewing and analyzing card transaction history
4. **Balance Management**: Monitoring and managing card balances

## UI Component Map

```
Flash BTCPayServer Plugin UI
├── Lightning Setup
│   └── Flash Lightning Tab
├── Navigation Menu
│   └── Flash Cards Entry
├── Flash Card Management
│   ├── Card List Page
│   ├── Card Registration Page
│   ├── Card Details Page
│   └── Card Transaction History
└── Flash Card Balance
    ├── Balance Check Page
    └── Top-up Page
```

## Lightning Setup Components

### Flash Lightning Tab

![Flash Lightning Tab](https://example.com/images/flash-lightning-tab.png)

**Path**: Store Settings > Lightning > Flash Tab

**Description**: A tab in the Lightning setup page that allows merchants to configure the Flash Lightning connection.

**Features**:
- Connection string configuration
- API key input
- Wallet selection
- Connection testing

**Implementation**: 
- Located in `Views/Shared/Flash/LNPaymentMethodSetupTab.cshtml`
- Rendered as part of BTCPayServer's Lightning setup UI

**Code Sample**:
```html
<template id="flash">
    <div class="accordion-item">
        <h2 class="accordion-header" id="CustomFlashHeader">
            <button type="button" class="accordion-button collapsed" data-bs-toggle="collapse" data-bs-target="#CustomFlashContent">
                <span><strong>Flash</strong> via GraphQL</span>
                <vc:icon symbol="caret-down"/>
            </button>
        </h2>
        <div id="CustomFlashContent" class="accordion-collapse collapse" aria-labelledby="CustomFlashHeader">
            <div class="accordion-body">
                <ul class="pb-2">
                    <li>
                        <code><b>type=</b>flash;<b>server=</b>https://api.flashapp.me/graphql;<b>api-key</b>=flash_...;<b>wallet-id=</b>xyz</code>
                    </li>
                </ul>
                <p class="my-2">Head over to the <a href="https://dashboard.flashapp.me" target="_blank">Flash dashboard</a> and create an API key.</p>
            </div>
        </div>
    </div>
</template>
```

## Navigation Components

### Flash Cards Navigation Entry

**Path**: Main Navigation

**Description**: A navigation menu entry that provides access to the Flash Cards management interface.

**Features**:
- Direct link to the Flash Cards page
- Visual icon for easy identification

**Implementation**: 
- Located in `Views/Shared/Flash/NavExtension.cshtml`
- Injected into BTCPayServer's navigation menu system

**Code Sample**:
```html
<a asp-controller="UIFlashCard" asp-action="Index" class="nav-link">
    <span class="nav-link-icon">
        <i class="fa fa-credit-card"></i>
    </span>
    <span class="nav-link-title">
        Flash Cards
    </span>
</a>
```

## Card Management Components

### Card List Page

**Path**: Flash Cards

**Description**: Main page for managing Flash cards, showing a list of all registered cards.

**Features**:
- Overview of all registered cards
- Status indicators (active/blocked)
- Quick actions for each card
- Card filtering and search
- Register new card button

**Implementation**: 
- Located in `Views/FlashCard/Index.cshtml`
- Controller: `UIFlashCardController.Index()`

**Wireframe**:
```
+------------------------------------------+
| Flash Cards                   [Register] |
+------------------------------------------+
| [ Search... ]                [ Filter ▼] |
+------------------------------------------+
| Card Name | UID      | Created  | Status |
+------------------------------------------+
| Staff 1   | ABC123   | Apr 26   | Active |
| Staff 2   | DEF456   | Apr 25   | Blocked|
| Customer  | GHI789   | Apr 24   | Active |
+------------------------------------------+
|                                1 2 3 ... |
+------------------------------------------+
```

### Card Registration Page

**Path**: Flash Cards > Register New Card

**Description**: Form for registering a new Flash card.

**Features**:
- Card name input
- Card scanning via NFC
- Manual UID entry
- Initial balance configuration
- Spending limit configuration

**Implementation**: 
- Located in `Views/FlashCard/Register.cshtml`
- Controller: `UIFlashCardController.Register()`

**User Flow**:
1. User navigates to the card registration page
2. User enters card name and optionally initial balance
3. User scans card with NFC reader or enters UID manually
4. User submits the form
5. System registers the card and returns to the card list

### Card Details Page

**Path**: Flash Cards > [Card Name]

**Description**: Detailed view of a specific card, showing all information and actions.

**Features**:
- Card information (name, UID, status)
- Current balance
- Transaction history
- Block/unblock actions
- Top-up option
- Card programming instructions

**Implementation**: 
- Located in `Views/FlashCard/Details.cshtml`
- Controller: `UIFlashCardController.Details(id)`

**Wireframe**:
```
+------------------------------------------+
| ← Back to Cards                          |
+------------------------------------------+
| Card: Staff 1                            |
| UID: ABC123                             |
| Status: Active                 [Block ▼] |
+------------------------------------------+
| Balance: 50,000 sats           [Top Up] |
+------------------------------------------+
| Transaction History                      |
+------------------------------------------+
| Date       | Amount   | Type    | Status |
+------------------------------------------+
| Apr 26     | -1,000   | Payment | Done   |
| Apr 25     | +10,000  | Top-up  | Done   |
| Apr 24     | -500     | Payment | Done   |
+------------------------------------------+
|                                1 2 3 ... |
+------------------------------------------+
```

## Balance Management Components

### Balance Check Page

**Path**: /flash-cards/balance

**Description**: Public page for checking card balance by scanning a card.

**Features**:
- Card scanning via NFC
- Balance display
- Transaction history (limited)
- Top-up option

**Implementation**: 
- Located in `Views/FlashBalance/ScanCard.cshtml` and `Views/FlashBalance/BalanceView.cshtml`
- Controller: `UIFlashBalanceController.ScanCard()` and `UIFlashBalanceController.GetBalanceView()`

**User Flow**:
1. User accesses the balance check page
2. User scans card with NFC reader
3. System displays card balance and recent transactions
4. User can optionally top up the card

### Top-up Page

**Path**: Flash Cards > [Card Name] > Top Up

**Description**: Form for adding funds to a card.

**Features**:
- Amount input
- Payment method selection
- Invoice generation
- Balance update confirmation

**Implementation**: 
- Located in `Views/FlashCard/TopUp.cshtml`
- Controller: `UIFlashCardController.TopUp(id)`

**User Flow**:
1. User navigates to the top-up page for a specific card
2. User enters amount to add
3. System generates a Lightning invoice
4. User pays the invoice
5. System adds funds to the card and returns to the card details page

## UI Interaction Patterns

### NFC Card Scanning

The plugin supports NFC card scanning in browsers that implement the Web NFC API:

```javascript
async function scanCard() {
    try {
        if ("NDEFReader" in window) {
            const reader = new NDEFReader();
            await reader.scan();
            
            reader.addEventListener("reading", ({ serialNumber }) => {
                const uid = Array.from(serialNumber)
                    .map(b => b.toString(16).padStart(2, '0'))
                    .join('');
                    
                document.getElementById("cardUid").value = uid;
                displaySuccess(`Card detected: ${uid}`);
            });
        } else {
            displayError("NFC not supported by this browser");
        }
    } catch (error) {
        displayError(`Error: ${error.message}`);
    }
}
```

### Real-time Balance Updates

Card balance updates are shown in real-time using SignalR:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/card-updates")
    .build();
    
connection.on("BalanceUpdated", (cardId, newBalance) => {
    if (currentCardId === cardId) {
        updateBalanceDisplay(newBalance);
    }
});

connection.start();
```

### Form Validations

All forms implement client-side validation:

```html
<form asp-action="Register" asp-controller="UIFlashCard" method="post">
    <div class="form-group">
        <label asp-for="CardName" class="form-label">Card Name</label>
        <input asp-for="CardName" class="form-control" />
        <span asp-validation-for="CardName" class="text-danger"></span>
    </div>
    
    <!-- Additional form fields -->
    
    <button type="submit" class="btn btn-primary">Register Card</button>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

## UI States

The UI components handle various states:

### Loading States

When data is being loaded or actions are being processed:

```html
<div class="loading-indicator" id="loadingIndicator">
    <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading...</span>
    </div>
</div>
```

### Error States

When errors occur during API calls or processing:

```html
<div class="alert alert-danger" id="errorAlert" role="alert" style="display: none;">
    <i class="fas fa-exclamation-circle"></i>
    <span id="errorMessage"></span>
</div>
```

### Empty States

When no data is available (e.g., no cards registered):

```html
<div class="empty-state">
    <div class="text-center my-5">
        <i class="fas fa-credit-card fa-4x text-muted mb-3"></i>
        <h4>No Flash cards registered yet</h4>
        <p class="text-muted">Register your first Flash card to get started</p>
        <a asp-action="Register" class="btn btn-primary mt-3">
            <i class="fa fa-plus"></i> Register New Card
        </a>
    </div>
</div>
```

## Responsive Design

All UI components are designed to be responsive, adapting to different screen sizes:

- **Desktop**: Full layout with all features visible
- **Tablet**: Adjusted layout with some elements reorganized
- **Mobile**: Simplified layout with focus on essential functions

Media queries and Bootstrap's responsive grid system are used:

```css
/* Desktop styles (default) */
.card-list-container {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 20px;
}

/* Tablet styles */
@media (max-width: 992px) {
    .card-list-container {
        grid-template-columns: repeat(2, 1fr);
    }
}

/* Mobile styles */
@media (max-width: 576px) {
    .card-list-container {
        grid-template-columns: 1fr;
    }
}
```

## Accessibility Considerations

The UI components follow accessibility best practices:

- Proper ARIA attributes for interactive elements
- Keyboard navigation support
- Sufficient color contrast
- Screen reader support
- Focus management

Example:

```html
<button 
    id="scanButton" 
    class="btn btn-primary" 
    aria-label="Scan NFC card"
    aria-describedby="scanHelpText">
    <i class="fas fa-credit-card" aria-hidden="true"></i>
    Scan Card
</button>
<small id="scanHelpText" class="form-text text-muted">
    Place your NFC card on the reader to scan
</small>
```

## Internationalization

The UI components support internationalization:

- Text extraction for translation
- RTL language support
- Date and number formatting

Example:

```html
<h2>@ViewLocalizer["Flash Cards"]</h2>
<p>@string.Format(ViewLocalizer["Last updated: {0}"], 
       Model.LastUpdated.ToString(CultureInfo.CurrentCulture))</p>
```

## Theming

The UI components respect BTCPayServer's theming system:

- Usage of BTCPayServer CSS variables
- Consistent design language
- Support for light and dark modes

## Progressive Enhancement

The UI follows a progressive enhancement approach:

1. **Basic Functionality**: Works without JavaScript
2. **Enhanced Experience**: Added features with JavaScript
3. **Advanced Features**: NFC support where available

## Conclusion

The UI components of the Flash BTCPayServer plugin provide a comprehensive and user-friendly interface for managing Flash cards and Lightning payments. The components are designed to be responsive, accessible, and consistent with BTCPayServer's design language.