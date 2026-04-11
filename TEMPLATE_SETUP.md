# Template Slide Setup Guide

## Overview

The `ExportService` identifies template slides in your source `.pptx` by their **slide name**
(the `name` attribute on the `<p:cSld>` element). Each template slide must have a name that
matches one of the constants defined in `ExportService`:

```csharp
public const string TemplateQuestion = "TPL_Question";
public const string TemplateAnswer   = "TPL_Answer";
```

The template slides are **not visible** in the final export — they are cloned for each output
slide and then deleted from the deck.

---

## Required Slide Structure

Each template slide must contain:

1. **Named shape placeholders** — text boxes whose shape name matches the keys used in
   `slide.Shapes`. For example, shapes named `Question`, `Answer`, `Position`, `Points`.
2. **A notes placeholder with any text** — even a single space. This is required so that the
   notes part is included in the cloned slide. Without it, the notes cannot be written correctly.

---

## Setting the Slide Name via VBA

Open your template `.pptx` in PowerPoint, then open the VBA editor (`Alt + F11`) and run the
following macro. Adjust the slide indices and names to match your deck.

```vba
Sub RenameSlide()

    Dim sld As Slide
    Dim currentName As String
    Dim newName As String

    Set sld = ActiveWindow.View.Slide
    currentName = sld.Name

    newName = InputBox("Enter new name for this slide:" & vbCrLf & _
        "(Slide " & sld.SlideIndex & ")", _
        "Rename Slide", currentName)

    ' Cancel or empty input ? abort
    If newName = "" Then
        MsgBox "Cancelled. No changes made.", vbExclamation
        Exit Sub
    End If

    sld.Name = newName
    ActivePresentation.Save

    MsgBox "Slide renamed to: " & newName, vbInformation

End Sub
```

You can verify the names without running a macro by checking the slide name in the Immediate
Window (`Ctrl + G` in the VBA editor):

```vba
? ActivePresentation.Slides(1).Name
```

---

## Adding the Required Notes Placeholder

For each template slide:

1. In PowerPoint, click on the slide to select it.
2. At the bottom of the screen, click **"Click to add notes"**.
3. Type any placeholder text — e.g. `PLACEHOLDER` or a single space.
4. Save the file.

This ensures the notes part exists in the XML and is correctly cloned by `ExportService`.
Without this step, the notes relationship will be missing from generated slides.

---

## Shape Naming

Shape names are set via the **Selection Pane** in PowerPoint:

`Home → Editing → Select → Selection Pane`

Click any shape in the pane to rename it. The name you assign here must exactly match the key
used in `slide.Shapes`:

```csharp
new Slide
{
    TemplateName = ExportService.TemplateAnswer,
    Shapes = new Dictionary<string, string>
    {
        ["Question"] = "In which state is Sydney located?",
        ["Answer"]   = "New South Wales",
        ["Position"] = "Question 2",
        ["Points"]   = ""
    },
    Notes = "99 correct answers"
}
```

Shape names are **case-sensitive**.

---

## Checklist Before Export

| Check | How to verify |
|---|---|
| Template slide has the correct name | VBA Immediate Window: `? ActivePresentation.Slides(n).Name` |
| All required shape names are set | Selection Pane in PowerPoint |
| Notes placeholder contains any text | Click the notes area below the slide |
| File is saved as `.pptx` | Save As → PowerPoint Presentation |

