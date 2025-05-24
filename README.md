# Dev

- Install [.NET SDK](https://dotnet.microsoft.com/en-us/download) (I use 8.0 LTS, should work with newer)

```powershell
# clone repo and navigate to it

# to start program
dotnet run

# to create .exe file
dotnet publish -c Release -r win-x64 --self-contained=false /p:PublishSingleFile=true
```
