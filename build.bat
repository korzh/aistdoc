cd src\aistdoc
dotnet pack -c Release
cd ..\..

mkdir dist
copy src\aistdoc\dist\*.nupkg dist\