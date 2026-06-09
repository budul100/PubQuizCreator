# PubQuizCreator

Self-hosted quiz management and export application.

## Overview

PubQuizCreator is a self-hosted web application for managing pub quiz questions, rounds, and templates, and for exporting quizzes as PowerPoint presentations or PDF documents. It runs as a Docker container and uses PostgreSQL with the pgvector extension for storing questions and semantic similarity search via Ollama embeddings.

## Technology Stack

| Component   | Technology                                         |
|-------------|----------------------------------------------------|
| Framework   | ASP.NET Core 9 / Blazor Server                     |
| Database    | PostgreSQL 16 with pgvector                        |
| Embeddings  | Ollama (`mxbai-embed-large`, 1024 dimensions)      |
| Export      | PowerPoint (.pptx), PDF                            |
| Container   | Docker / Docker Compose                            |
| Registry    | GitHub Container Registry (ghcr.io)                |

---

## Deployment

### Prerequisites

- Docker and Docker Compose installed on the host
- A reverse proxy network named `proxynet` must exist
- Ollama reachable from within the container (configured via `Ollama:BaseUrl`)

### Services (`docker-compose.yml`)

| Service       | Description                                                                 |
|---------------|-----------------------------------------------------------------------------|
| `pubquiz-db`  | PostgreSQL 16 with pgvector, internal network only, health-checked          |
| `pubquiz-web` | Blazor application, exposed via `proxynet`, depends on db health check      |

### Environment Variables (`.env`)

| Variable                        | Description                                                    |
|---------------------------------|----------------------------------------------------------------|
| `DB_PASSWORD`                   | PostgreSQL password for the `pubquiz` user                     |
| `ConnectionStrings__Default`    | Full Npgsql connection string                                  |
| `Auth__Username` / `Auth__Password` | Basic auth credentials for the web interface              |
| `Media__StoragePath`            | Absolute path to the media directory inside the container      |
| `Ollama__BaseUrl`               | Ollama endpoint, e.g. `http://host.docker.internal:11434`      |
| `Export__TemplatesPath`         | Absolute path to the templates directory inside the container  |

### `docker-compose.yml` Example

```yaml
services:
  pubquiz-db:
    image: pgvector/pgvector:pg16
    restart: unless-stopped
    environment:
      POSTGRES_USER: pubquiz
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: pubquiz
    volumes:
      - db_data:/var/lib/postgresql/data
    networks:
      - internal
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pubquiz"]
      interval: 10s
      timeout: 5s
      retries: 5

  pubquiz-web:
    image: ghcr.io/budul100/pubquizcreator:latest
    restart: unless-stopped
    env_file: .env
    volumes:
      - ${MEDIA_PATH}:/app/media
      - ${TEMPLATES_PATH}:/app/templates
      - ./settings.override.json:/app/settings.override.json
    depends_on:
      pubquiz-db:
        condition: service_healthy
    networks:
      - internal
      - proxynet

networks:
  internal:
  proxynet:
    external: true

volumes:
  db_data:
```

### Required Files on the Host

Place the following in `/opt/pubquizcreator/` before starting:

```
.env                    # environment variables
docker-compose.yml      # production compose file
media/                  # directory for uploaded media files
templates/              # directory for PPTX template files
settings.override.json  # runtime settings overrides
```

### Starting the Application

```bash
cd /opt/pubquizcreator
docker compose pull
docker compose up -d
```

Database migrations run automatically on startup. The application is available via the configured reverse proxy.

### Updating a Running Instance

Deployment to the server is not automated. An `update.sh` script can be placed in `/opt/pubquizcreator/` for convenience:

```bash
#!/usr/bin/env bash
set -eu

echo "=== Pulling latest images ==="
docker compose pull --quiet

echo "=== Starting / updating containers ==="
docker compose up -d

echo "=== Removing old dangling images ==="
docker image prune -f

echo "=== Update finished successfully ==="
```

Run it manually when a new image is available:

```bash
cd /opt/pubquizcreator && bash update.sh
```

Every push to `main` triggers a GitHub Actions workflow that builds a `linux/arm64` Docker image and pushes it to `ghcr.io/budul100/pubquizcreator:latest`. The image is publicly available and can be pulled without authentication.

To trigger an update via GitHub Actions manually (without SSH access), a `workflow_dispatch` workflow can be added to the repository.

---

## Features

| Area               | Details                                                                                                    |
|--------------------|------------------------------------------------------------------------------------------------------------|
| Question management | Create, edit, categorize, and tag questions with media (image, audio, video). Tracks usage history and supports semantic duplicate detection via Ollama embeddings. |
| Quiz planning      | Build quizzes from rounds and slots. Assign questions per category, drag-and-drop reordering, round templates. |
| Export             | Generates PowerPoint presentations (questions and answers decks) from configurable `.pptx` templates. PDF export also available. |
| AI integration     | Configurable fact-check prompt with placeholders for question and answer text. Opens a configurable AI URL in the browser. |

---

## PowerPoint Template Setup

Exports use two `.pptx` or `.potx` template files: one for the questions deck and one for the answers deck. These are uploaded via the Settings page and stored on the server.

### Slide Identification

The export service identifies template slides by their slide name, set in the XML attribute `name` on the `p:cSld` element. Each template file must contain slides with the following exact names:

| Slide Name    | Purpose                              |
|---------------|--------------------------------------|
| `TPL_Question` | Template slide for question slides  |
| `TPL_Answer`   | Template slide for answer slides    |

Template slides are not included in the final export. They are cloned for each output slide, then removed from the deck.

### Setting the Slide Name

Open the template file in PowerPoint and use the VBA editor (`Alt+F11`) to rename slides. Run the following macro with the target slide active:

```vba
Sub RenameSlide()
    Dim sld As Slide
    Set sld = ActiveWindow.View.Slide
    Dim newName As String
    newName = InputBox("New name:", "Rename Slide", sld.Name)
    If newName = "" Then Exit Sub
    sld.Name = newName
    ActivePresentation.Save
End Sub
```

To verify slide names without a macro, use the VBA Immediate Window (`Ctrl+G`):

```vba
? ActivePresentation.Slides(1).Name
```

### Required Shape Names

Each template slide must contain text box shapes with names matching the keys used by the export service. Shape names are **case-sensitive**. Set them via the Selection Pane:

> **Home > Editing > Select > Selection Pane** — click a shape in the pane to rename it.

| Shape Name            | Content                                                            |
|-----------------------|--------------------------------------------------------------------|
| `Question`            | The text of the question                                           |
| `QuestionDescription` | The text of the question and the description                       |
| `Answer`              | The text of the answer                                             |
| `Position`            | Slide title / question number (formatted via `Export:TitleFormat`) |
| `Media`               | Placeholder for media content (image, audio, video)                |

### Notes Placeholder

Each template slide must have a notes placeholder with at least one character of text (a space is sufficient). Without it, the notes XML part is absent from the cloned slide and the export service cannot write notes to it.

To add it: select the slide in PowerPoint, click the notes area below the slide, and type any text. Save the file.

### Template Checklist

| Check | How to verify |
|---|---|
| Slide name set correctly | VBA Immediate Window: `? ActivePresentation.Slides(n).Name` |
| All required shapes named | Selection Pane in PowerPoint; names are case-sensitive |
| Notes placeholder has content | Click notes area below slide; at least one character required |
| File saved as `.pptx` or `.potx` | **Save As > PowerPoint Presentation or Template** |

---

## Local Development

A separate `docker-compose.dev.yml` is provided for local development. It starts PostgreSQL on port `5433` and Ollama on port `11434`. The web application runs directly from Visual Studio or the `dotnet` CLI against the Development connection string in `appsettings.Development.json`.

The Ollama embedding model must be pulled manually before first use:

```bash
ollama pull mxbai-embed-large
```

The application runs without Ollama. Embedding generation is skipped gracefully when Ollama is unavailable, and the status indicator in the navigation bar reflects the current connectivity state.

---

## License

This project is licensed under the [MIT License](LICENSE).
