# Clear contents of folders
Remove-Item -Path 'Publish' -Recurse
Remove-Item -Path 'PublishSelfContained' -Recurse

# Create publish folders
[System.IO.Directory]::CreateDirectory([System.IO.Path]::Combine($PWD.Path, 'Publish'))
[System.IO.Directory]::CreateDirectory([System.IO.Path]::Combine($PWD.Path, 'PublishSelfContained'))

# Publish game normally
dotnet publish 'CSMaze\CSMaze.csproj' /p:PublishProfile="CSMaze\Properties\PublishProfiles\FolderProfile.pubxml"
dotnet publish 'CSMazeConfigEditor\CSMazeConfigEditor.csproj' /p:PublishProfile="CSMazeConfigEditor\Properties\PublishProfiles\FolderProfile.pubxml"
dotnet publish 'CSMazeDesigner\CSMazeDesigner.csproj' /p:PublishProfile="CSMazeDesigner\Properties\PublishProfiles\FolderProfile.pubxml"
dotnet publish 'CSMazeServer\CSMazeServer.csproj' /p:PublishProfile="CSMazeServer\Properties\PublishProfiles\FolderProfile.pubxml"
dotnet publish 'CSMaze\CSMaze.csproj' /p:PublishProfile="CSMaze\Properties\PublishProfiles\FolderProfile.pubxml"

# Publish game self-contained
dotnet publish 'CSMaze\CSMaze.csproj' /p:PublishProfile="CSMaze\Properties\PublishProfiles\FolderProfileSelfContained.pubxml"
dotnet publish 'CSMazeConfigEditor\CSMazeConfigEditor.csproj' /p:PublishProfile="CSMazeConfigEditor\Properties\PublishProfiles\FolderProfileSelfContained.pubxml"
dotnet publish 'CSMazeDesigner\CSMazeDesigner.csproj' /p:PublishProfile="CSMazeDesigner\Properties\PublishProfiles\FolderProfileSelfContained.pubxml"
dotnet publish 'CSMazeServer\CSMazeServer.csproj' /p:PublishProfile="CSMazeServer\Properties\PublishProfiles\FolderProfileSelfContained.pubxml"
dotnet publish 'CSMaze\CSMaze.csproj' /p:PublishProfile="CSMaze\Properties\PublishProfiles\FolderProfileSelfContained.pubxml"

# Delete debugging symbols
Remove-Item -Path 'Publish\*.pdb'
Remove-Item -Path 'PublishSelfContained\*.pdb'

# Delete unnecessary file
Remove-Item -Path 'PublishSelfContained\CSMaze.runtimeconfig.json'
