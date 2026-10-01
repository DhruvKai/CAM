# Filling CAM with your own content using Copilot

CAM ships with **no cards** (Label Legends) and **no questions** (Cyber Trivia). You add them as JSON files.
Copilot can write those files for you from your spreadsheet or document.

On first start, CAM creates a `Data` folder next to `CAM.exe` containing:

| File | What it is |
|---|---|
| `cards.json` | Label Legends cards. Starts empty (`[]`). **Replace this.** |
| `cards.sample.json` | Five example cards showing the format. Never loaded by the game. |
| `quizQuestions.json` | Cyber Trivia questions. Starts empty (`[]`). **Replace this.** |
| `quizQuestions.sample.json` | Four example questions showing the format. Never loaded by the game. |

The same two sample files are in this repository under `src/DataSortingGame.Core/Defaults/`.

---

## 1. Label Legends cards from the Excel sheet

Attach **the Excel workbook** and **`cards.sample.json`** to Copilot, then paste:

```text
I've attached an Excel workbook of data classification scenarios and a file called cards.sample.json.
The workbook has one sheet per classification (Public, Internal, Confidential, Highly Restricted).
Each sheet has the columns "Scenario shown to players", "Classification", "Why" and "Comments".

Convert every row from every sheet into one JSON array, in exactly the same format as cards.sample.json.
Each card has these four fields:
- "id": unique, lowercase, no spaces. Number each sheet separately: pub-001, pub-002 ... for Public,
  int-001 ... for Internal, con-001 ... for Confidential, hr-001 ... for Highly Restricted.
- "scenario": the "Scenario shown to players" text, copied exactly.
- "categoryId": must be one of "public", "internal", "confidential", "restricted",
  taken from the Classification column ("Highly Restricted" becomes "restricted").
- "why": the "Why" text, copied exactly.

Rules:
- Ignore the Comments column and skip empty rows.
- Do not rewrite, shorten or summarise any text. Fix only broken line breaks inside a cell.
- Output only valid JSON (no comments, no trailing commas) in a single code block.
- After the code block, tell me how many cards there are for each categoryId.
```

Check the counts Copilot reports against the number of rows on each sheet.

## 2. Cyber Trivia questions from a PDF or text file

Attach **your questions document** and **`quizQuestions.sample.json`** to Copilot, then paste:

```text
I've attached a document of quiz questions and a file called quizQuestions.sample.json.

Convert every question into one JSON array, in exactly the same format as quizQuestions.sample.json.
Each question has these six fields:
- "id": q001, q002, q003 ... in the order the questions appear.
- "text": the question, copied exactly.
- "options": the answer choices in the order given, without their "A." / "b)" / "1." prefixes.
  Between 2 and 4 options. A True/False question has the two options "True" and "False".
- "correctIndex": the position of the correct answer in "options", COUNTING FROM 0:
  first option = 0, second = 1, third = 2, fourth = 3.
- "difficulty": 1 = easy, 2 = medium, 3 = tricky. Use the document's difficulty if it gives one;
  otherwise judge it yourself and aim for a mix of all three.
- "explanation": one or two sentences on why the answer is correct. Use the document's explanation
  if there is one; otherwise write a short, factual one.

Rules:
- If a question has more than 4 options, or no clear correct answer, leave it out and list it
  after the code block so I can fix it.
- Output only valid JSON (no comments, no trailing commas) in a single code block.
- After the code block, list every id with the full text of its correct answer, so I can check
  that each correctIndex points at the right option.
```

Read through the id/answer list Copilot gives you. A wrong `correctIndex` is the most likely mistake.

## 3. Load the files into CAM

1. Open `Data\cards.json` (or `Data\quizQuestions.json`) in Notepad, select everything, and paste
   Copilot's JSON over it. Save it as UTF-8 (Notepad's default).
2. In CAM, open **Admin** (PIN) and click **Reload settings**, or restart the app.
3. Check **Admin > Settings warnings**. Any card or question that was skipped is listed there with the reason
   (for example a duplicate id, an unknown categoryId or an out-of-range correctIndex).
4. The file summary in Admin shows how many cards and questions were loaded.

You can also fix individual entries afterwards in **Admin > Manage cards** and **Admin > Manage quiz questions**.

## Field reference

**Card** (`cards.json`)

| Field | Required | Notes |
|---|---|---|
| `id` | yes | Unique. Never shown to players. |
| `scenario` | yes | Shown on the card. Long text is fine: the card shrinks the font to fit. |
| `categoryId` | yes | `public`, `internal`, `confidential` or `restricted`. The category name (e.g. `"Highly Restricted"`) also works. |
| `why` | recommended | Shown after the player answers and on the results screen. |

**Quiz question** (`quizQuestions.json`)

| Field | Required | Notes |
|---|---|---|
| `id` | yes | Unique. |
| `text` | yes | The question. |
| `options` | yes | 2 to 4 non-empty answers. |
| `correctIndex` | yes | 0 = first option, 1 = second, 2 = third, 3 = fourth. |
| `difficulty` | recommended | 1 easy (green), 2 medium (yellow), 3 tricky (red). Defaults to 2. |
| `explanation` | recommended | Shown after the answer is revealed. |
