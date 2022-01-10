# Report Generator

## Introduction

The **Report Generator** is the program we use to automate about 40% of an accessibility audit. After being given correct filepaths and canvas API tokens, this program has logic to scan HTML for many possible accessibility issues we often find in courses. This markdown will give a description of each file used in the process, pseudo code for each file, as well as pseudo code for the whole report generation.

## Audit Process

1. Receive Course to audit.
2. **Run Report Generator** through the Command Panel GUI.
3. Briefly review generator excel report.
4. Go through course by hand and add findings to the report previously generated.
5. Fix findings and report completed items (update the excel report continuously).
6. Return accessibility audit.

## High level Pseudo Code

```
BEGIN
CreateReport() (WPFCommandPanel/GenReportEvents.cs)
    Test conditions of course (WPFCommandPanel/GenReportEvents.cs, CanvasAPI.cs)
        IF conditions not met THEN
            Report not generated
        ENDIF
    Create data structure (RGeneratorBase.cs)
    FOR each page
        Find accessibility issues (A11yParser.cs)
        Get media information (MediaParser.cs)
    ENDFOR
    Translate data found to report using excel template (CreateExcelReport.cs)
    Return Report (WPFCommandPanel/GenReportEvents.cs)
END
```

## Project Files and Short Descriptions

**[WPFCommandPanel/GenReportEvent.cs](source/GenReportEvents.cs)**
: `CreateReport()` class serves as main function and Initiation of the report generation process. It contains the parent algorithm.

**[A11yParser.cs](source/A11yParser.cs)**
: This files contains the logic for finding the accessibility issues. It is the "meat" of the report generator. A list of automated findings is found in [A11yParser Logic](A11yParser%20Logic.md).

**[LinkParser.cs](source/LinkParser.cs)**
: This file is used to parse through links in a directory of html files. It is not used while looking in canvas courses. The Parser takes a given `<a>` html tag and returns the title, URL, and a status of wether the URL is working or receiving a number of HTML errors

**[MediaParser.cs](source/MediaParser.cs)**
: The Media Parser sorts through all videos displayed in a course via embed video and specific video links finding Video ids, video lengths, closed captions, and transcripts. The following are video domains that the Media Parser is designed to search from: Brightcove, Kanoby, Alexander Street, BYU mediasite, Panopto, Youtube, Ambrose, Facebook, DailyMotion, and Vimeo. The Media Parser can also find information from inline video's.

**[VideoParser.cs](source/VideoParser.cs)**
: The Video Parser is a class of methods used to parse through videos and gain their information.

**[CreateExcelReport.cs](source/CreateExcelReport.cs)**
: Converts findings from A11yParser and MediaParser into rows in the [Excel Accessibility Review Template](CAR%20-%20Accessibility%20Review%20Template.xlsx) (Sheet: Accessibility Review) using a data structure from [RGeneratorBase.cs](source/RGeneratorBase.cs).

**[PanelOptions.cs](source/PanelOptions.cs)**
: File/dir paths & user data

**[CanvasApi.cs](source/CanvasApi.cs)**
: classes to hold canvas info

**[CourseDataStructures.cs](source/CourseDataStructures.cs)**
: translates canvas data into a usable format

**[SeleniumExtensions.cs](source/SeleniumExtentions.cs)**
: Selenium - used to find media data by traversing the HTML.

**[StringExtensions.cs](source/StringExtentions.cs)**
: Helpful String Methods. Splits strings into useful information.

# A11yParser Logic

## Automated Findings

Below is a list of the automated issues found by the report Generator. In the A11yParser file methods for each top level issue is called in the `ProcessContent()` function. The second levels are found using if/else statements and added to a list defined in the RParserBase class (lines 29 - 27) in RGeneratorBase.cs and the PageData class in CourseDataStructures.cs (lines 313 - 351)

- **Image**
  - **Banners**
  - **Insufficient alt text**
  - **alt text contains filenames**

- **Color**
  - **Color Contrast insufficient**

- **Keyboard**

- **Screen Reader**
  - **Iframes**

- **Media**

- **Semantics**
  - **Headers**
  - **Misuses of HTML tags**

- **Links**
  - **JavaScript Links are not accessible**
  - **Empty Link tag**
  - **Invisible Link (No visible text)**
  - **Adjust Link Text**

- **Table**
  - **Stretched Cells**
  - **No Headers**
  - **No scope attributes**
  - **Empty table**
  - **Complex tables**

This is the current organization of accessibility issues we have so far. That being said, this organizational list is subject to change and we plan on implementing a better organization soon.
