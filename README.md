# CLMS
An attempt to build a fully working LMS library written in c#.

## Requirements
* NET 5.0
* Syroot.BinaryData v2.0.2

## Setting up
Install the required Syroot.BinaryData version using this:
`install-Package -version 2.0.2 Syroot.IO.BinaryData`

## Features

### General
* Changing the endianess, encoding and version.

### MSBT file format
* Editing of the messages (including tag editing), attributes and style indices.

### MSBP file format
* Editing of the colors, the attribute infos, the control tags, the styles and the source files.

### WMBP file format
* Reading of the languages and the fonts.

## Credits
* [Trippixyz](https://github.com/Trippixyz): Project Leader, Programmer, Tester, Github Manager
* [KillzXGaming](https://github.com/KillzXGaming): Implementation of other msbt types
* [Kinnay](https://github.com/kinnay): [Reversing most of the files](https://github.com/Kinnay/Nintendo-File-Formats/wiki/LMS-File-Format)
* Ray Koopa: Syroot Library Developer
* [Jenrikku](https://github.com/Jenrikku): Help with setting up a c# library, Motivation on pushing this to Github finally
