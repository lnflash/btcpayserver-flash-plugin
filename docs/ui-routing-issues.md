# UI Routing Issues and Resolution

This document details the current routing issues in the Flash plugin for BTCPay Server and proposes solutions.

## Current State (v1.3.6)

The Flash plugin for BTCPay Server has been updated with an improved UI and functionality, but there are routing issues that prevent some pages from loading correctly:

1. **Working Routes**:
   - `/plugins/flash` - Main dashboard (displays correctly)
   - Form submissions within working pages (function correctly)

2. **Non-working Routes**:
   - `/plugins/flash/boltcard` - Returns 404 error

## Issue Description

The primary issue is that BTCPay Server's routing mechanism isn't correctly mapping the URL `/plugins/flash/boltcard` to the appropriate controller action and view.

### Implementation Details

The current implementation uses:

1. **Controller Route**:
   ```csharp
   [Route("plugins/flash")]
   public class UIFlashController : Controller
   {
       [HttpGet("boltcard")]
       public IActionResult Boltcard() { ... }
   }
   ```

2. **View Location**:
   - Views/UIFlash/Boltcard.cshtml

3. **Navigation Registration**:
   ```csharp
   applicationBuilder.AddUIExtension("header-nav", "Flash/BasicNav");
   ```

## Root Cause Analysis

The 404 error likely occurs due to one of the following issues:

1. **View Engine Convention Mismatch**: BTCPay Server's MVC configuration might be looking for views in a different location than where they are placed.

2. **Route Registration Issue**: The plugin's route registration might not be compatible with BTCPay Server's expected format.

3. **Controller Naming Convention**: There might be a mismatch between the controller name "UIFlashController" and BTCPay Server's controller discovery mechanism.

4. **ASP.NET Core Areas**: If BTCPay Server uses MVC Areas for plugins, our implementation isn't properly registering as an area.

## Solution Approaches

### Approach 1: Adjust Controller Naming and Route Format

Update the controller to follow BTCPay Server's naming conventions:

```csharp
[Route("plugins/flash")]
public class FlashController : Controller
{
    [HttpGet]
    public IActionResult Index() { ... }

    [HttpGet("boltcard")]
    public IActionResult Boltcard() { ... }
}
```

Move view files from `Views/UIFlash/` to `Views/Flash/` to match the controller name.

### Approach 2: Use Route Override Attribute

Explicitly define the full route path for each action:

```csharp
[Route("plugins")]
public class UIFlashController : Controller
{
    [HttpGet("flash")]
    public IActionResult Index() { ... }

    [HttpGet("flash/boltcard")]
    public IActionResult Boltcard() { ... }
}
```

### Approach 3: Register as MVC Area

Implement as an MVC Area with appropriate registration:

```csharp
public class FlashAreaRegistration : AreaRegistration
{
    public override string AreaName => "Flash";

    public override void RegisterArea(AreaRegistrationContext context)
    {
        context.MapRoute(
            "Flash_default",
            "plugins/flash/{controller}/{action}/{id}",
            new { controller = "Home", action = "Index", id = UrlParameter.Optional }
        );
    }
}
```

### Approach 4: Study and Mimic Breez Plugin

Carefully examine the Breez plugin implementation which is known to work correctly:

1. Examine controller route attributes
2. Check view path conventions
3. Analyze UI extension registration
4. Review navigation structure

## Implementing the Solution

### Strategy for Implementation

1. **Create FlashController**:
   - Create a new controller named FlashController (without the "UI" prefix)
   - Implement with the same route attributes as the Breez plugin
   - Copy the action methods from UIFlashController

2. **Move View Files**:
   - Rename/move view files to match BTCPay Server's conventions
   - Create Views/Flash/ directory for views
   - Ensure view names match action method names

3. **Update Navigation Registration**:
   - Adjust UI extension registration to match Breez's approach
   - Ensure menu item points to the correct controller/action

### Implementation Steps

1. **Create New Controller**:
   ```csharp
   [Route("plugins/{storeId}/Flash")]
   [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
   public class FlashController : Controller
   {
       [HttpGet("")]
       public IActionResult Index(string storeId) { ... }

       [HttpGet("boltcard")]
       public IActionResult Boltcard(string storeId) { ... }
   }
   ```

2. **Move View Files**:
   - Move Views/UIFlash/Boltcard.cshtml to Views/Flash/Boltcard.cshtml
   - Move Views/UIFlash/Index.cshtml to Views/Flash/Index.cshtml
   - Update view imports and references as needed

3. **Update Plugin Registration**:
   ```csharp
   applicationBuilder.AddUIExtension("store-integrations-nav", "Flash/_Nav");
   applicationBuilder.AddScoped<Controllers.FlashController>();
   ```

## Testing Recommendations

After implementing these changes, test with the following steps:

1. **Incremental Testing**:
   - Test each change individually to identify which one resolves the issue
   - Start with just the controller name and route changes
   - Then test view locations
   - Finally test navigation registration

2. **Verification Points**:
   - Main dashboard (`/plugins/flash/`) loads correctly
   - Boltcard page (`/plugins/flash/boltcard`) loads correctly
   - Navigation menu links to the correct URLs
   - Forms submit to the correct endpoints

3. **Diagnostics**:
   - Monitor BTCPayServer logs during startup
   - Check for controller registration messages
   - Look for view resolution errors
   - Monitor HTTP requests and responses

## Fallback Options

If the recommended approach doesn't work, consider these fallbacks:

1. **Alternative Routes**: Implement multiple route attributes on controllers to try different URL patterns
2. **Direct View Rendering**: Use `return View("~/Views/Specific/Path/Boltcard.cshtml")` to bypass MVC conventions
3. **JavaScript Navigation**: Use JavaScript redirection for problematic routes
4. **Custom Navigation Items**: Create custom HTML links instead of using ASP.NET Core tag helpers

## Conclusion

The routing issues in the Flash plugin are likely due to conventions mismatch between our implementation and BTCPayServer's expectations. By studying and mimicking the Breez plugin's approach, we should be able to resolve these issues and provide a seamless navigation experience.

Once the routing issues are fixed, the plugin will offer a complete and user-friendly interface for Boltcard topup functionality within BTCPay Server.