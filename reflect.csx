#r "/home/lee/.nuget/packages/blazormonaco/3.5.0/lib/net8.0/BlazorMonaco.dll"
using System.Reflection;
using BlazorMonaco.Editor;

var type = typeof(StandaloneCodeEditor);
foreach(var prop in type.GetProperties()) {
    if (prop.Name.Contains("OnKey") || prop.Name.Contains("Command") || prop.Name.Contains("Event")) {
        Console.WriteLine(prop.Name);
    }
}
foreach(var method in type.GetMethods()) {
    if (method.Name.Contains("AddCommand") || method.Name.Contains("Key")) {
        Console.WriteLine(method.Name);
    }
}
