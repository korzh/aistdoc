# AistDoc utility
Aistdoc allows to publish API reference for your .NET code on the web via [Aistant](https://aistant.com) documentation hosting service.

## Problem
AistDoc does the following 3 tasks:
 1. It collects all information about your code: the list namespaces, structures, interfaces, enums and classes. Methods and properties of the classes and interfaces. 
 
 2. It generates Markdown (or HTML) files which represents the full reference of your API (one file for each class, interface or enum). If the [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments) are used - they are added to the generated documenation as well.
 
 3. It publishes those files on the web using [Aistant](https://aistant.com) service as a hosting platform.
 
As the result you get a nice website with a API Reference for your code. Since Aistant is a general-purpose service for managing knowledge bases and help centers - you can add your own articles which describe the general things about your library: basic concepts, code samples, use cases, etc.


## How to use
Documentation for Aistant contains [detailed instructions](https://docs.aistant.com/en/tutorials/publish-api-reference-net-class-library) on how to apply AistDoc to your project.
 
 

