# AistDoc utility

## What is AistDoc

AistDoc is an API documentation generator and publishing tool for your code. Currently it supports:
 * .NET with [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments)
 * TypeScript with code comments in [TypeDoc](https://typedoc.org/) format (a superset of JSDoc).

Main AistDoc features:

 1. It collects all information about your code: the list of namespaces, structures, interfaces, enums, and classes as well as methods and properties of the classes and interfaces. 
 
 2. It generates Markdown (or HTML) files which represent the full reference of your API (one file for each class, interface or enum). 
 If your code contains [XML documentation](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments) (for .NET projects) or [TypeDoc](https://typedoc.org/) (for TypeScript) comments - then those comments will be added to the generated articles as well.     
 __NB__:For TypeScript projects, you will need to process your code with TypeDoc tool first. 
 
 3. It publishes the documentation generated on step #2 on the Web using [Aistant](https://aistant.com) service as a hosting platform.
 
As a result, you will get a nice website with full API reference of your code. Since Aistant is a general-purpose service for managing knowledge bases and help centers - you can add to that documentation your own articles with basic concepts of your project, tutorials, code samples, etc .


## Installation

Aistdoc is implemented as a .NET Core Global Tool, so its installation is quite simple:

```
dotnet tool install -g Aistant.DocImport
```

To update it to the latest version, if it was installed previously, use:

```
dotnet tool update -g Aistant.DocImport
```

Of course you need [.NET Core SDK](https://dotnet.microsoft.com/download) (version 2.1 or higher) be installed on your computer first.


## Tutorials

For details on how to publish your documentation please read one of our tutorials:

 * [Publishing the API reference for your .NET code](https://docs.aistant.com/en/tutorials/publish-api-reference-net-class-library)

 * [Publishing the API reference for your TypeScript project with TypeDoc](https://docs.aistant.com/en/tutorials/publish-api-reference-typescript-typedoc)
 


