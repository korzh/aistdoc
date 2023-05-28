# AistDoc utility

| Build status | Nuget|
|---|---|
|   [![Build status](https://dev.azure.com/korzhdev/EasyQuery/_apis/build/status/aistdoc/prod-aistdoc)](https://dev.azure.com/korzhdev/EasyQuery/_build/latest?definitionId=41)| [![NuGet](https://img.shields.io/nuget/v/Aistant.DocImport.svg)](https://www.nuget.org/packages/Aistant.DocImport) |

## 1. What is AistDoc?

AistDoc is an API documentation generator and publishing tool for your code. Currently it supports:

 * .NET with [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments)

 * TypeScript with code comments in [TSDoc](https://github.com/Microsoft/tsdoc) format (a superset of JSDoc).

Main AistDoc features:

 1. It collects all information about your code: the list of namespaces, classes, enums and  interfaces as well as all methods and properties of those classes and interfaces. 
 
 2. It generates Markdown (or HTML) files with full API reference for your code (one file for each class, interface or enum). 
 If your code contains [XML documentation](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments) (for .NET projects) or [TSDoc](https://github.com/Microsoft/tsdoc) (for TypeScript) comments then they will be added to the generated articles as well.     

 __NB1__: For TypeScript projects, you will also need to process your code with [TypeDoc](https://typedoc.org) tool first.    
 
 __NB2__: Here you can find the [detailed description of TSDoc comments format](https://typedoc.org/guides/doccomments/). 
 
 3. It can publish the documentation generated on step #2 on the Web using [Aistant](https://aistant.com) service as a hosting platform.
 
As a result, you will get a nice website with full API reference of your code. Since Aistant is a general-purpose service for managing knowledge bases and help centers,you can add other articles to that documentation (e.b. basic concepts of your project, tutorials, code samples, etc).

4. Additionally, AistDoc allows to generate a changelog document (here is [an example](https://github.com/KorzhCom/EasyData/releases/tag/v1.2.2)) by the commit comments of a special format (like `[Fix] A very serious bug was fixed in this release` or `[New] .NET 5 support was added`)

## 2. Installation

Aistdoc is implemented as a .NET Core Global Tool, so the installation is quite simple:

```
dotnet tool install -g Aistant.DocImport
```

To update it to the latest version use:

```
dotnet tool update -g Aistant.DocImport
```

__NB__: Of course you need [.NET Core SDK](https://dotnet.microsoft.com/download) (version 3.1 or higher) be installed on your computer (Linux, Windows or Mac) first.


## 3. Generating documentation for .NET code

There are two ways to generate documentation based on your code:
- using assembly DLLs and corresponding XML files
- using NuGet packages (.nupkg files)

We will cover both cases below more in detail:

### 3.1 Generating documentation by assemblies

#### 3.1.1 Prepare your library files

* Make sure you turned on "XML documentation file" option in all projects for which you plan to publish the documentation.
* Build all projects in your library.
* Place all assemblies (DLL files) and XML documentation files to one folder.


#### 3.1.2 Create a configuration file

__Aistdoc__  reads all settings from a special JSON configuration file. To generate a template for that file use the following command:

```
aistdoc create -f:<config file name> -m:cs 
``` 

Here `-f` (or `--file`) parameter allows to specify the name of the config file and `-m` (or `--mode`) specifies the mode (either `ts` for TypeScript or `cs` for C#).

For example, the following command

```
aistdoc create -f:mylib.json -m:cs 
``` 

will create `mylib.json` config file in the current folder.

The config file has 2 main sections: `aistant` and `source`.   
The first one contains the credentials to your Aistant account, the ID of the knowledge base and the URL to the section where your documentation will be published. You can skip this section if you don't plat to publish your documentation to Aistant.com.

The second one includes the path to your assemblies and XML files with the documenation.

All properties are well commented. Here is an example of the configuration file:


```javascript 
{
  "source": {
    "mode":"csharp",
    "path": "C:\\Projects\\MyLibrary\\dist",
    "filter": {
		  "assembly": "^MyCompany\\.MyLibrary",
		  "namespace": ""
    }   
  }
}
```

The latest section (`filter`) of the config file contains a regular expression that allows to specify the filter for assemblies and/or namespaces. 

The filter from the example above means that __AistDoc__ will take only the assemblies started with *MyCompany.MyLibrary* and will generate the docs for all classes with any namespace within those files.

#### 3.1.3 Publish the documentation

Finally, use `publish` command to publish your documentation to some folder (for example, `docs/`): 

```
aistdoc publish --config:mylib.json --output:docs
```

### 3.2 Generating documentation by NuGet packages

#### 3.2.1 Prepare your assemblies and packages

* Make sure you turned on "XML documentation file" option in all projects for which you plan to publish the documentation.
* Build and pack (`dotnet pack`) all necessary projects and put all .nupkg files to one folder.

#### 3.2.2 Create a configuration file

To generate a conifguration file, you can follow the steps described in the previous section.

In this case you can remove `path` and `filter` options. Set `packages` option as a path to the folder with `.nupkg` files.

```javascript 
{
  "source": {
    "mode":"csharp",
    "packages": "C:\\Projects\\MyLibrary\\packages"
  }
}
```

#### 3.2.3 Publish the documentation

Finally, use `publish` command to publish your documentation to some folder: 

```
aistdoc publish --config:mylib.json --output:docs
```

## 4. Generating documentation for TypeScript code

### 4.1 Install Node.js

In this scenario we will use [TypeDoc](https://typedoc.org/) which is installed via NPM, so you need [Node.js](https://nodejs.org/en/) with NPM installed first.

### 4.2 Install TypeDoc

[TypeDoc](https://typedoc.org/) tool allows you to parse your TypeScript code with [TSDoc](https://github.com/Microsoft/tsdoc) comments and generate JSON files with the full API reference of your library. After that those generated files will be passed to AistDoc for publishing. To install TypeDoc just run this command:
```
npm install --global typedoc
```

### 4.3 Documente your code with TSDoc

TypeDoc utility will scan your TypeScript code and gather information about all code structures (modules, namespaces, classes, interfaces, enums, etc) to a special JSON file. 
If you what to add some descriptions to all those structures you can comment your code with a special comments in [TSDoc format](https://typedoc.org/guides/doccomments/) (a superset of JSDoc).


### 4.4 Run TypeDoc to get JSON files

When all installations are done and your code is properly commented - you might proceed to the next step and generate the documentation for your project.   
Use the following command to get a JSON file with API reference for one package of your code:

```
typedoc --out docs /src
```

You can also apply TypeDoc during your building process with WebPack or other build tools. Take a look at [TypeDoc installation guide](https://typedoc.org/guides/installation/) for more information.

### 4.5 Create a configuration file for __AistDoc__

Similar to the previous cases, we need a configuration file that will "tell" AistDoc which files to proceed and where to publish the result.
To generate a template for that file use the following command:

```
aistdoc create -f:<config file name> -m:ts 
``` 

Here `-f` (or `--file`) parameter allows to specify the name of the config file and `-m` (or `--mode`) defines the mode (either `ts` for TypeScript or `cs` for C#).

For example, the following command

```
aistdoc create -f:mylib.json -m:ts 
``` 

will create `mylib.json` config file in the current folder.

The config file has 2 main sections: `aistant` and `source`.   
The first one contains the credentials to your Aistant account, the ID of the knowledge base and the URL to the section where your documentation will be published.
You can skip it if you don't plan to publish your documentation on Aistant.com.

The second one includes the path to the TypeDoc JSON files generated on the previous step.

All properties are well commented. Here is an example of the configuration file:

```javascript 
{
  "source": {
    "mode": "typescript", //the type of the source (either "csharp" or "typescript")

    //TypeDoc JSON files. Required.
    "files": [
      "docs/my-package1.json",
      "docs/my-package2.json"
	  ]
  }
}
```

### 4.6 Publish the documentation

Finally, use `publish` command to publish your documentation to some folder. 
Example: 

```
aistdoc publish --config:mylib.json --output:docs
```

## 5. Publishing documentation to Aistant.com 

For details on how to publish your documentation please read one of our tutorials:

 * [Publishing the API reference for your .NET code](https://docs.aistant.com/en/tutorials/publish-api-reference-net-class-library)

 * [Publishing the API reference for your TypeScript project with TSDoc](https://docs.aistant.com/en/tutorials/publish-api-reference-typescript-tsdoc)
 
## 6. Generating Changelog

__Aistdoc__ allows you to create a Changelog document based on Git commits. To be processed a commit message must match the following pattern:

```
[TYPE] Some text 
```

Here TYPE can be one of the following:
- __fix__ - indicates that the commit contains fixes.
- __upd__ - indicates that the commit contains some modifications or improvements (changes in the API for example).
- __new__ - indicates that the commit contains new features.
- __doc__ - indicates that the commit contains documentation changes.

To separate the changes between different versions __Aistdoc__ uses version tags, so please be sure that your repository contains them.

### 6.1 Create a configuration file

As for other tasks we start with creating a configuration file that will "tell" __AistDoc__ where to get the necessary information and where to publish the result.

```
aistdoc create -f:<config file name> -m:git 
``` 

Here `-f` (or `--file`) parameter allows to specify the name of the config file and `-m` (or `--mode`) defines the mode.

The configuration file includes 2 main sections: `aistant` and `git`.

The first one contains the credentials to your Aistant account, the ID of the knowledge base and the URL for root documentation section. You can skip it if you don't need to publish your Changelog with Aistant.

The second section (`git`) includes all information about your Git credentials and the projects you are going to generate Changelog for.

Here is an example of the configuration file:

```javascript
 "git": {

    // Array of credentials
    "credentials": [
      {
        "id": "default",

        "userName": "my_UserName",
        "password": "my_Password",

        // you can use access token instead of userName and password
        "accessToken": "PAT",

        //indicates wether this credential will be used as default, if reporsitory section 
        // does not have credentialId defined.
        "default": true
      }
    ],

    // Array of projects
    "projects": [
      {
        // project id
        "id": "my_project_id",

        //project tag
        "tag": "my_project_tag",

        // the template for the title that starts the list of changes for one version
        "titleTemplate": "Version ${VersionNum}",

        // the template for change description
        "logItemTemplate": "__[${ItemType}]__: ${ItemTitle}    ${ItemDescription}\n",

        // release date template
        "dateItemTemplate": "${ReleasedDate}\n",

        // skip title with version
        "skipVersionHeading": false,

        //changelog url for Aistant
        "changelog": "",

        // repositories of the current project
        "repositories": [
          {
            // the ID of the Git credentials defined in "credentials" section above
            "credentialId": "default",

            // local path
            "path": "my_path",

            // git repo remote url
            "url": "",

            // the branch
            "branch": "master",

            // clones repo from remote url if path does not exist
            "cloneIfNotExist": "false",     
          }
        ]
      }
    ]
  }
```

### 6.2 Publish the Changelog

To publish the changelog for a particular version use the following command: 
```
aistdoc changelog <project id> -c:<config file name> --pat:<PAT> -v:<version> -o:<filename>
```

Here:

- `<PAT>` - personal access token. It's an optional parameter. You can add it if the access token is not listed in the configuration file in `accessToken` option (for security reasons for example).

- `<version>` - the version number for which we want to generate our changelog

- `<filename>` - the output file. It will be a text file in Markdown format with all changes made between the specified version and the previous one.

For example:
```
aistdoc changelog my_project_id -c:mygit.json -v:1.0.0 -o:changelog.md
```


## Enjoy!

And don't forget to add your star to this GitHub repository if you've found aistdoc useful.


