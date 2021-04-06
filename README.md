# AistDoc utility

## What is AistDoc

AistDoc is an API documentation generator and publishing tool for your code. Currently it supports:
 * .NET with [XML documentation comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments)
 * TypeScript with code comments in [TSDoc](https://github.com/Microsoft/tsdoc) format (a superset of JSDoc).

Main AistDoc features:

 1. It collects all information about your code: the list of namespaces, structures, interfaces, enums, and classes as well as methods and properties of the classes and interfaces. 
 
 2. It generates Markdown (or HTML) files which represent the full reference of your API (one file for each class, interface or enum). 
 If your code contains [XML documentation](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/xml-documentation-comments) (for .NET projects) or [TSDoc](https://github.com/Microsoft/tsdoc) (for TypeScript) comments - then those comments will be added to the generated articles as well.     
 __NB1__: For TypeScript projects, you will also need to process your code with [TypeDoc](https://typedoc.org) tool first.    
 __NB2__: Here you can find the [detailed description of TSDoc comments format](https://typedoc.org/guides/doccomments/). 
 
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

__NB__: Of course you need [.NET Core SDK](https://dotnet.microsoft.com/download) (version 3.1 or higher) be installed on your computer (Linux, Windows or Mac) first.


## Publish C# docs localy

There are two ways to generate documentation based on your code:
- using DLL and XML files
- using Nuget packages


#### __DLL and XML files__
### Preparing your library files
* Make sure you turned on "XML documentation file" option in all projects for which you plan to publish the documentation.
* Build all projects in your library.
* Place all assemblies (DLL files) and XML documentation files to one folder.

### Configuration file
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
The first one contains the credentials to your Aistant account, the ID of the knowledge base and the URL to the section where your documentation will be published. You can remove it for now.

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

The latest (`filter`) section of the config file contains a regular expression which filters necessary assemblies and/or namespaces 

The filter from the example above means that __AistDoc__ takes all assemblies started with *MyCompany.MyLibrary* and will generate the docs for all classes with any namespace within those files.

Finally, use `publish` command to publish your documentation to the folder `docs`, for example: 

```
aistdoc publish --config:mylib.json --output:docs
```

#### __Nuget packages__
### Preparing your library files
* Make sure you turned on "XML documentation file" option in all projects for which you plan to publish the documentation.
* Build all projects in your library.
* Publsih your projects as nuget packages to the same folder.

### Configuration file
To generate conifguration file, you can follow steps described in previous method.

For this approach you can remove `path` and `filter` options. Set `packages` options as path to the folder with `.nupkg` files.

```javascript 
{
  "source": {
    "mode":"csharp",
    "packages": "C:\\Projects\\MyLibrary\\packages"
  }
}
```

Finally, use `publish` command to publish your documentation to the folder `docs`, for example: 

```
aistdoc publish --config:mylib.json --output:docs
```

## Publish TypeScript docs localy
### 1. Install Node.js
In this scenario we will use [TypeDoc](https://typedoc.org/) which is installed via NPM, so you need [Node.js](https://nodejs.org/en/) with NPM installed first.

### 2. Install TypeDoc
[TypeDoc](https://typedoc.org/) tool allows you to parse your TypeScript code with [TSDoc](https://github.com/Microsoft/tsdoc) comments and generate JSON files with the full API reference of your library. After that those generated files will be passed to AistDoc for publishing. To install TypeDoc just run this command:
```
npm install --global typedoc
```
### 3. Documenting your code with TSDoc
TypeDoc utility will scan your TypeScript code and gather information about all code structures (modules, namespaces, classes, interfaces, enums, etc) to a special JSON file. 
If you what to add some descriptions to all those structures you can comment your code with a special comments in [TSDoc format](https://typedoc.org/guides/doccomments/) (a superset of JSDoc).

### 4. Running TypeDoc to get JSON files
When all installations are done and your code is properly commented - you might proceed to the next step and generate the documentation for your project.   
Use the following command to get a JSON file with API reference for one package of your code:

```
typedoc --out docs /src
```

You can also apply TypeDoc during your building process with WebPack or other build tools. Take a look at [TypeDoc installation guide](https://typedoc.org/guides/installation/) for more information.

### 5. Configure aistdoc

Finally we need to configure our aistdoc tool to tell it which files to proceed and where to publish the result.
__Aistdoc__  reads all settings from a special JSON configuration file. To generate a template for that file use the following command:

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
You can remove it for now.

The second one includes the path to your assemblies and XML files with the documenation.

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

The latest (`files`) section of the config file contains the list of paths to JSON files generated on the previous step 3.

Finally, use `publish` command to publish your documentation to the folder `docs`, for example: 

```
aistdoc publish --config:mylib.json --output:docs
```

## Publish to Aistant

For details on how to publish your documentation please read one of our tutorials:

 * [Publishing the API reference for your .NET code](https://docs.aistant.com/en/tutorials/publish-api-reference-net-class-library)

 * [Publishing the API reference for your TypeScript project with TSDoc](https://docs.aistant.com/en/tutorials/publish-api-reference-typescript-tsdoc)
 
## Generating changelog
__Aistdoc__ supports creating changelog based on special commits. Such commit must have name that mathes the following pattern: 
```
[TYPE] Commit name 
```

There are several types of sgpecial commits:
- __FIX__ indicates that commit contains fixes.
- __UPD__ indicates that commit contains updates (changes in API for example).
- __NEW__ indicates that commit contains new features.
- __DOC__ indicates that commit contains documentation changes.

To separate changes between different versions __Aistdoc__ uses version tags, so be sure that your repository contains them.

### Configuration file
We need to configure our aistdoc tool to tell it which projects to proceed and where to publish the result.

__Aistdoc__  reads all settings from a special JSON configuration file. To generate a template for that file use the following command:

```
aistdoc create -f:<config file name> -m:git 
``` 

Here `-f` (or `--file`) parameter allows to specify the name of the config file and `-m` (or `--mode`) defines the mode.

For example, the following command

```
aistdoc create -f:mygit.json -m:git 
``` 

will create `mygit.json` config file in the current folder.

The config file has 2 main sections: `aistant` and `git`.   
The first one contains the credentials to your Aistant account, the ID of the knowledge base and the URL to the section where your documentation will be published.
You can remove it for now.

The second one includes git settings.

All properties are well commented. Here is an example of the configuration file:
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

        // title template for version changes
        "titleTemplate": "Version ${VersionNum}",

        // change description template
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
            // credentials
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

To publish changelog you can use the following command: 
```
aistdoc changelog <project id> -c:<config file name> --pat:<PAT token> -v:<version> -o:<filename>
```

`<PAT token>` is optional. The equivalent in configuration file is `accessToken`. It is used in case you don't want to save security info in the configuration file.

`<version>` is the version num for which you generate documentation

`<filename>` the output file.

For example:
```
aistdoc changelog my_project_id -c:mygit.json -v:1.0.0 -o:changelog.md
```

