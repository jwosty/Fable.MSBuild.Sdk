# Fable.Sdk

NOTE: This is a highly experimental proof-of-concept. Use at your own risk!

Adds a custom Fable SDK for MSBuild, which allows for easy building of Fable projects (read more about custom SDKs here: https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk?view=vs-2022).

## What does it do?

*The package is not yet available on NuGet.org - to make it accessible, you must first package the SDK with `dotnet pack`, then add it to a custom feed you control (for example a [local feed](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds), or a locally running [BaGet instance](https://github.com/loic-sharma/BaGet)). Don't forget to make your NuGet feed available to your Fable project (usually via a NuGet.config)!*

First, just import the SDK into your fsproj:

```xml
<Project Sdk="Fable.Sdk/0.0.1">
    <!-- ... -->
</Project>
```

Next, target Fable:

```xml
<!-- ... -->
    <PropertyGroup>
        <TargetFramework>fable4.0</TargetFramework>
    </PropertyGroup>
<!-- ... -->
```

Now, `dotnet build` will perform the Fable build for you - no need to invoke [`fable`](https://www.nuget.org/packages/Fable) tool yourself. Magical!

## Sample projects

NOTE: `clean_all.sh` will completely clean your local NuGet cache - any globally cached packages will need to be re-downloaded after this. This is necessary because NuGet caches the SDK the first time it gets restored, and changes that you make to the SDK don't invalidate the cache. The only way around this is to change the version every time you make a change, or to clear the package cache.

To try it out, clone this repo and then run `./clean_all.sh && build_test_projects.sh`. This will first pack the SDK as a nuget package, and then use it to build one of the test projects under [./test_projects/](./test_projects/) (referencing the built SDK using a local directory feed).

There's no documentation yet. If you want to see what properties and targets are available, poke around in this folder: https://github.com/jwosty/Fable.Sdk/tree/master/src/Fable.Sdk/Build
