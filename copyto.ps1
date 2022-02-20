if ($args.Count -lt 1)
{
    echo "Enter output dir";
    return;
}

$path = "";
for($i = 0; $i -lt $args.Count; $i++)
{
    $path += $args[$i];
}

$pwd = pwd;
 
$files = Get-ChildItem -Path $pwd -Recurse -Filter *.cs -Exclude ".\RustPlugins\bin",".\RustPlugins\obj",".\.vs",".\.idea",".NETCoreApp,Version=v6.0.AssemblyAttributes.cs","RustPlugins.AssemblyInfo.cs","RustPlugins.GlobalUsings.g.cs",".NETFramework,Version=v4.8.AssemblyAttributes"

foreach($file in $files)
{
    Copy-Item -Recurse -Force -Path $file.FullName -Destination $path;
}