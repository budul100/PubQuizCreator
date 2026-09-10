# PubQuizCreator

Self-hosted quiz management and export application.

## Overview

PubQuizCreator is a self-hosted web application for managing pub quiz questions, rounds, and templates, and for exporting completed quizzes as PowerPoint presentations, PDF documents, or JSON data[cite: 1, 4]. It uses PostgreSQL with the pgvector extension for storing questions and performing semantic similarity searches via Ollama embeddings[cite: 1, 2].

## Technology Stack

| Component  | Technology                                    |
| ---------- | --------------------------------------------- |
| Framework  | ASP.NET Core 9 / Blazor Server[cite: 1, 4]                |
| Database   | PostgreSQL 16 with pgvector[cite: 1]                   |
| Embeddings | Ollama (`mxbai-embed-large`, 1024 dimensions)[cite: 1, 2] |
| Export     | PowerPoint (.pptx), PDF (QuestPDF), JSON[cite: 2, 4]      |
| Container  | Docker / Docker Compose[cite: 1]                       |
| Registry   | GitHub Container Registry (ghcr.io)           |

---

## Deployment

### Prerequisites

- Docker and Docker Compose installed on the host
- An external Docker bridge network named `proxynet` (e.g. for a reverse proxy)[cite: 1]
- Ollama accessible from within the container (configured via `Ollama__BaseUrl`)[cite: 3]

### Services (`docker-compose.yml`)

| Service       | Description                                                            |
| ------------- | ---------------------------------------------------------------------- |
| `pubquiz-db`  | PostgreSQL 16 with pgvector, internal network only, health-checked[cite: 1]     |
| `pubquiz-web` | Blazor application, connected to `proxynet` and the internal db network[cite: 1] |

### Environment Variables (`.env`)

| Variable                            | Description                                                   |
| ----------------------------------- | ------------------------------------------------------------- |
| `DB_PASSWORD`                       | PostgreSQL password for the `pubquiz` database user[cite: 1]           |
| `ConnectionStrings__Default`        | Full Npgsql connection string[cite: 1]                                 |
| `Auth__Username` / `Auth__Password` | Cookie authentication credentials for the web interface[cite: 3]       |
| `Media__StoragePath`                | Absolute path to the media folder inside the container[cite: 3]        |
| `Export__TemplatesPath`             | Absolute path to the PPTX templates folder[cite: 3]                    |
| `Ollama__BaseUrl`                   | Ollama API endpoint (e.g., `[http://host.docker.internal:11434](http://host.docker.internal:11434)`)[cite: 1, 3] |

### Production `docker-compose.yml` Example

```yaml
services:
  pubquiz-db:
    image: pgvector/pgvector:pg16
    container_name: pubquiz-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: pubquiz
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: pubquiz
    volumes:
      - pubquiz-pgdata:/var/lib/postgresql/data
    networks:
      - pubquiz-internal
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pubquiz -d pubquiz"]
      interval: 10s
      timeout: 5s
      retries: 5

  pubquiz-web:
    image: ghcr.io/budul100/pubquizcreator:latest
    container_name: pubquiz-web
    restart: unless-stopped
    env_file: .env
    volumes:
      - ./media:/data/media
      - ./templates:/data/templates
      - ./settings.override.json:/app/settings.override.json
    extra_hosts:
      - "host.docker.internal:host-gateway"
    depends_on:
      pubquiz-db:
        condition: service_healthy
    networks:
      - pubquiz-internal
      - proxynet

networks:
  pubquiz-internal:
    internal: true
  proxynet:
    external: true

volumes:
  pubquiz-pgdata:
```

### Directory Setup

Prepare the working directory (e.g. `/opt/pubquizcreator/`)[cite: 1]:

```text
├── .env
├── docker-compose.yml
├── media/                  # Uploaded images, audio, and videos
├── templates/              # Uploaded PPTX/POTX presentation templates
└── settings.override.json  # Runtime configuration overrides (created automatically)
```

Start the containers:

```bash
docker compose pull
docker compose up -d
```

---

## Features

| Area                | Description                                                                                                                                                         |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Question Management | Create, edit, and categorize questions with attachments (image, audio, video)[cite: 1]. Tracks usage history across quizzes and performs vector-based duplicate detection[cite: 2]. |
| Idea Inbox          | Quick-capture brainstorming ideas (with optional media) before converting them into fully formulated quiz questions[cite: 1, 3].                                               |
| Quiz Planning       | Assemble quizzes by rounds and slots[cite: 1, 4]. Support for round templates, category coverage insights, and drag-and-drop ordering[cite: 2, 4].                                         |
| Multi-format Export | Export selected rounds into styled PowerPoint decks (`.pptx` or bundled `.zip`), printable overview sheets (`.pdf`), or structured data (`.json`)[cite: 4].               |
| AI Fact-Checking    | One-click clipboard prompt compilation for external AI review (e.g. Gemini, ChatGPT) using customizable prompt templates[cite: 3].                                           |

---

## PowerPoint Template Setup

Presentations are generated from `.pptx` or `.potx` templates uploaded in **Settings**[cite: 2, 4]. The export service clones and populates slides based on predefined slide and shape names[cite: 1, 2].

### Slide Names

The template should contain slides named via the `name` attribute in the presentation XML (`p:cSld`)[cite: 2]:

| Slide Name | Purpose                                                          |
| ---------- | ---------------------------------------------------------------- |
| `Question` | Default slide for regular text questions[cite: 1, 2]                         |
| `Media`    | Slide for questions containing image, audio, or video attachments[cite: 1, 2] |
| `Answer`   | Optional answer template slide[cite: 1, 2]                                   |

*Note: Template slides are cloned for each question slot and automatically removed from the exported presentation[cite: 2].*

#### Setting Slide Names in PowerPoint (VBA)

Use the VBA editor (`Alt + F11`) in PowerPoint to assign names to the template slides:

```vba
Sub RenameCurrentSlide()
    Dim sld As Slide
    Set sld = ActiveWindow.View.Slide
    Dim newName As String
    newName = InputBox("Enter slide name (e.g. Question, Media):", "Rename Slide", sld.Name)
    If newName <> "" Then sld.Name = newName
End Sub
```

To verify a slide name in the Immediate Window (`Ctrl + G`):

```vba
? ActivePresentation.Slides(1).Name
```

### Required Shape Names

Shape names are **case-sensitive**. Rename shapes using PowerPoint's **Selection Pane** (*Home > Editing > Select > Selection Pane*):

| Shape Name            | Target Content                                                 |
| --------------------- | -------------------------------------------------------------- |
| `Title`               | Slide title (e.g., question number formatted via `TitleFormat`)[cite: 1, 2] |
| `Question`            | Short question text[cite: 1, 2]                                            |
| `QuestionDescription` | Extended description / notes[cite: 1, 2]                                   |
| `Answer`              | Answer text[cite: 1, 2]                                                    |
| `Media`               | Picture/media placeholder for attached image, audio, or video[cite: 1, 2]  |

---

## Local Development

Start the local database and Ollama instance using the provided Compose profile[cite: 1]:

```bash
docker compose -f docker-compose.dev.yml up -d
```

- **PostgreSQL:** Port `5433` (Password: `dev`)[cite: 1]
- **Ollama:** Port `11435`[cite: 1]

Pull the embedding model locally:

```bash
ollama pull mxbai-embed-large
```

Launch the web app directly from your IDE or via CLI[cite: 4]:

```bash
dotnet run --project PubQuizCreator.Web --launch-profile "PubQuizCreator (local)"
```

---

## License

This project is licensed under the [MIT License](LICENSE).