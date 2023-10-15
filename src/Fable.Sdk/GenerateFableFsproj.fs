namespace Fable.Sdk.Tasks
open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Fable.Sdk.Tasks.Patterns

type GenerateFableFsproj() =
    inherit Task()
    
    let indent = String.replicate 4 " "
    let itemStringSep = Environment.NewLine + indent
    
    let getCustomMetadataDict (taskItem: ITaskItem) =
        let customMetadataNonGeneric = taskItem.CloneCustomMetadata()
        let customMetadata =
            customMetadataNonGeneric.Keys
            |> Seq.cast<string>
            |> Seq.map (fun k -> k, string (customMetadataNonGeneric[k]))
        customMetadata
    
    let mutable cts: CancellationTokenSource option = None
        
    [<Required>]
    member val Sources: ITaskItem[] = Array.empty with get, set
    
    [<Required>]
    member val PackageReferences: ITaskItem[] = Array.empty with get, set
    
    [<Required>]
    member val ProjectReferences: ITaskItem[] = Array.empty with get, set
    
    [<Required>]
    member val OutputFsproj: string = "" with get, set
    
    override this.Execute () =
        this.Log.LogMessage (MessageImportance.Normal, "Writing Fable tool fsproj: {0}...", this.OutputFsproj)
        
        let tfm = "net7.0"
        
        let compileItems =
            this.Sources
            |> Array.map (fun sourceFile -> "..\\" + sourceFile.ItemSpec)
            |> Array.map (fun sourceFile -> $"""<Compile Include="{sourceFile}" />""")
        let compileItemsStr = compileItems |> String.concat itemStringSep
        
        let packageReferences =
            this.PackageReferences
            |> Array.map (fun pkgRef ->
                let customMetadata = getCustomMetadataDict pkgRef
                let attrs =
                    customMetadata
                    |> Seq.map (fun (k,v) -> $"%s{k}=\"%s{v}\"")
                    |> String.concat " "
                $"""<PackageReference Include="%s{pkgRef.ItemSpec}" %s{attrs}/>"""
            )
        
        let packageReferencesStr =
            packageReferences
            |> String.concat itemStringSep
        
        let projectReferences =
            this.ProjectReferences
            |> Array.map (fun projRef ->
                // this.Log.LogMessage (MessageImportance.High, sprintf $"projRef.ItemSpec: %s{projRef.ItemSpec}")
                // let originalPath = FileInfo(projRef.ItemSpec)
                // this.Log.LogMessage (MessageImportance.High, sprintf $"originalPath: %O{originalPath}")
                // let newPath = "..\\" + originalPath.Directory.FullName + "\\obj\\" + originalPath.Name
                this.Log.LogMessage (MessageImportance.High, $"Environment.CurrentDirectory: %s{Environment.CurrentDirectory}")
                let originalPath = projRef.ItemSpec
                // Since it's MSBuild convention to use Windows-style path separators in project files, it's okay that
                // we use concatenation (THE HORROR) instead of using Path.Combine
                let newPath =
                    if Path.IsPathFullyQualified originalPath then
                        //    C:\Code\App\src\Dependency\Dependency.fsproj
                        // -> C:\Code\App\src\Dependency\obj\Dependency.fsproj
                        Path.Combine (Path.GetDirectoryName originalPath, "obj", Path.GetFileName originalPath)
                    else
                        //       ..\Dependency\Dependency.fsproj
                        // -> ..\..\Dependency\obj\Dependency.fsproj
                        Path.Combine ("..", Path.GetDirectoryName originalPath, "obj", Path.GetFileName originalPath)
                this.Log.LogMessage (MessageImportance.High, $"Fixing up ProjectReference: %s{originalPath} -> %s{newPath}")
                let customMetadata = getCustomMetadataDict projRef
                let attrs =
                    customMetadata
                    |> Seq.map (fun (k,v) -> $"%s{k}=\"%s{v}\"")
                    |> String.concat " "
                $"""<ProjectReference Include="%s{newPath}" %s{attrs} />"""
            )
            
        let projectReferencesStr =
            projectReferences
            |> String.concat itemStringSep
        
        let fsprojText = $"""
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup>
<TargetFramework>{tfm}</TargetFramework>
</PropertyGroup>

<ItemGroup>
    {compileItemsStr}
</ItemGroup>

<ItemGroup>
    {projectReferencesStr}
</ItemGroup>

<ItemGroup>
    {packageReferencesStr}
</ItemGroup>
</Project>
"""
            
        File.WriteAllText (this.OutputFsproj, fsprojText)
        
        not this.Log.HasLoggedErrors
