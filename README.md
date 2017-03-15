## Shaman.Reflection.Exposer
Exposes private members of objects or types as `dynamic`.

Usage:
```csharp
using Shaman.Runtime.ReflectionExtensions;

dynamic exposed = someObject.Expose();
dynamic val = exposed._value;
dynamic ret = exposed.PrivateMethod();

typeof(SomeType).ExposeStatics().SomePrivateMethod();

```
