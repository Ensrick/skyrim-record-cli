# Contributing

Keep commands non-interactive and machine-readable. Before opening a pull
request, run:

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
```

Tests and examples must use synthetic fixtures. Do not commit Bethesda masters,
plugins from other authors, or local load-order data.
