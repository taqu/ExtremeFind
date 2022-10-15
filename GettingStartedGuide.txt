# Overview
Indexed full-text searching extension for Visual Studio (not Code). 
Indexing provides fast search instead of the first time indexing time and extra disk space. 

## Performance

|          |                        |
|:---------|:-----------------------|
| Files    | 36,438                 |
| Lines    | 5,767,194              |
| Indexing | 1,348,258 milliseconds |
| DB Size  | about 600 MiB          |

# Features

- Search
  - Perfect matching
- For results of search
  - Jump to the matched line
- Others
  - Automatically traverse all files in a solution
  - Cache indexed dates for each documents

# How to use
Open "Search Window" from [View] -> [Other Windows] -> [Search Window],

![](./doc/SearchWindow.jpg)

## Settings

|                  | Description                                   | Default     |
|:-----------------|:----------------------------------------------|:------------|
| Extensions       | Extensions which will be included in indexing | c cpp h ... |
| Max Search Items | Max number of items in each search            | 1000        |
