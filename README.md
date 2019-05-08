# AistDoc utility

## What is AistDoc
AistDoc is an API documentation generator and publishing tool for your code. Currently it supports:
 * .NET with [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments)
 * TypeScript with code comments in [TypeDoc](https://typedoc.org/) format (a superset of JsDoc).

AistDoc can help with the following tasks:
 1. It collects all information about your code: the list of namespaces, structures, interfaces, enums, and classes as well as methods and properties of the classes and interfaces. 
 
 2. It generates Markdown (or HTML) files which represent the full reference of your API (one file for each class, interface or enum). 
 If it's a .NET project and you use [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments) are then they are added to the generated articles as well.    
 For TypeScript projects, you will need to process your code with TypeDoc tool first. 
 
 3. It publishes the documentation generated on step #2 on the Web using [Aistant](https://aistant.com) service as a hosting platform.
 
As a result, you get a nice website with an API reference for your code. Since Aistant is a general-purpose service for managing knowledge bases and help centers - you can add your own articles which describe the general things about your code library: basic concepts, code samples, use cases, etc.


## How to use
Aistdoc is implemented as a .NET Core Global Tool, so its installation is quite simple:

```
dotnet tool install -g Aistant.DocImport
```

To update it to the latest version, if it was installed previously, use:

```
dotnet tool update -g Aistant.DocImport
```

For details on how to publish your documentation please read one of our tutorials:
 * [Publishing the API reference for your .NET code](https://docs.aistant.com/en/tutorials/publish-api-reference-net-class-library)
 * [Publishing the API reference for your TypeScript project with TypeDoc](https://docs.aistant.com/en/tutorials/publish-api-reference-typescript-typedoc)
 


