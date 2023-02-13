# CLMS
> <b>C</b>ool <b>L</b>ibrary <b>M</b>essage <b>S</b>tudio recreation

An attempt to build a fully working LMS library written in c#.something style bolded

## Requirements
* NET 5.0
* Syroot.BinaryData v2.0.1

## Setting up
If it doesnt automatically install the required Syroot.BinaryData versions:
`install-Package -version 2.0.1 Syroot.IO.BinaryData`

## Features

### General
* Changing the endianess, encoding and version.

### MSBT file format
* Editing of the messages (including tag editing), attributes and style indices.
* Experimental yaml format (msyt) for converting between .msbt and .yaml. This makes msbt files humanly readable while being just text(yaml).

### MSBP file format
* Editing of the colors, the attribute infos, the control tags, the styles and the source files.

### MSBF file format
* Reading of the fLow charts and the refernce labels.

### WMBP file format
* Reading of the languages and the fonts.

## Credits
* [Trippixyz](https://github.com/Trippixyz): Project Leader, Programmer, Tester, Github Manager
* [KillzXGaming](https://github.com/KillzXGaming): Implementation of other msbt types
* [Kinnay](https://github.com/kinnay): [Reversing most of the files](https://github.com/Kinnay/Nintendo-File-Formats/wiki/LMS-File-Format)
* Ray Koopa: Syroot Library Developer
* [Jenrikku](https://github.com/Jenrikku): Help with setting up a c# library, Motivation on pushing this to Github finally
