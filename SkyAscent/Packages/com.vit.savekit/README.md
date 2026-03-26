# ViT SaveKit

## How to use

1. Create DTO for your service need save.
2. Implement one or more `ViT.SaveKit.Abstractions.ISaveable` adapters.
3. Build the runtime save system. If you do not provide an account, SaveKit uses default Account `guest_001`:
4. Call `LoadAll()` on startup, then `SaveAll()` when needed.

### Example:

Step 1: Create DTO for your service. (if not yes).
```csharp
public sealed class SomethingData
{
    // Something...
}
```

Step 2: Create adapter and implement ISaveable.
```csharp
using ViT.SaveKit.Abstractions;

public sealed class SomethingSaveAdapter : ISaveable
{
    private SomethingManager manager;
    
    public string Key => "something"; // File name (MUST BE UNIQUE)

    public object Capture()
    {
        // Capture state to DTO for SaveSystem to serialize
        // ex: 
        return manager.CaptureSomethingData();
    }
    
    public void Restore(object data, int version)
    {
        // Apply data to runtime state
        // ex: 
        if (data is SomethingData dto)
        { 
            manager.ApplySomethingData(dto);
        }
    }
    
    // other implement of ISaveable ...
}

```

Step 3: Register service that you need to save with save system.
```csharp
using ViT.SaveKit.Runtime;

ISaveSystem saveSystem;

// ex: Applycation awake
saveSystem = SaveKitFactory.CreateLocalJson( 
    Application.persistentDataPath,
    
    // Adapter
    new SomethingSaveAdapter(player),
    new ...
    );
    
// ex: Application start
saveSystem.LoadAll();
    
// ex: Application quit
saveSystem.SaveAll();
```

## Optional custom account

```csharp
var accountContext = new MyAccountContext("user_123");
ISaveSystem saveSystem = SaveKitFactory.CreateLocalJson(
    accountContext,
    Application.persistentDataPath,
    new PlayerStatSaveAdapter(player));
```
