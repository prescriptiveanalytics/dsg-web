# DSG Web

REST API around the [DataStreamGenerator](https://github.com/janzenisek/dsg) library.
The API starts/stops a generated MQTT data stream and accepts the stream configuration as JSON.

## Docker image

Run the latest release:

```bash
docker pull ghcr.io/prescriptiveanalytics/dsg-web:latest
docker run -d -p 8080:8080 ghcr.io/prescriptiveanalytics/dsg-web:latest
```

### Tags

| Tag      | Points to                          |
| -------- | ---------------------------------- |
| `dev`    | The latest commit on `main`.       |
| `latest` | The latest release.                |
| `0.1.0`  | Release `0.1.0`.                   |
| `0.1`    | The latest patch release of `0.1`. |
| `0`      | The latest release of `0`.         |

## Run with Docker Compose

```bash
docker compose up --build -d
```

This starts:

| Service     | Endpoint                |
| ----------- | ----------------------- |
| `web`       | http://localhost:8080   |
| `mosquitto` | `localhost:1883`        |

Check the API:

```bash
curl http://localhost:8080/api/stream/status
```

Stop everything:

```bash
docker compose down
```

## Configure and start a stream

`POST` a configuration, then start the stream:

```bash
curl -X POST http://localhost:8080/api/stream/config \
  -H "Content-Type: application/json" \
  -d @- <<'JSON'
{
  "generator": {
    "id": "plantX",
    "environment": "PRODUCTION",
    "seed": 42,
    "interval": 100,
    "dateTimeFormat": "yyyy-MM-dd-HH-mm-ss-fff",
    "startDateTime": "2026-01-01T00:00:00",
    "decimalPrecision": 4,
    "duration": 60000,
    "type": "stream1",
    "brokerHostName": "mosquitto",
    "brokerHostPort": 1883
  },
  "series": [
    {
      "seriesType": "ME",
      "id": "s1",
      "export": true,
      "delay": 5,
      "rank": 1,
      "title": "s1",
      "topic": "raw/plantx/s1",
      "interval": 200,
      "initialValue": 0,
      "c": 0,
      "mean": 0,
      "stdDev": 0.1,
      "arguments": ["i"],
      "expression": "sin(i/5.0)"
    }
  ]
}
JSON

curl -X POST http://localhost:8080/api/stream/start
```

> **Broker host inside Compose:** the generator runs inside the `web` container, so
> `brokerHostName` must be `mosquitto` (the Compose service name), not `localhost`.
> Subscribers on the host connect to `localhost:1883`.

A posted configuration only takes effect on the next start. Posting a new
configuration while a stream is running marks it as pending until the stream is
restarted (`stop` + `start`).

## API

| Method | Path                 | Description                                      |
| ------ | -------------------- | ------------------------------------------------ |
| POST   | `/api/stream/config` | Store a configuration (takes effect on restart). |
| GET    | `/api/stream/config` | Read the stored configuration.                   |
| POST   | `/api/stream/start`  | Start the stream.                                |
| POST   | `/api/stream/stop`   | Stop the stream.                                 |
| GET    | `/api/stream/status` | Current stream status.                           |

## Development

```bash
dotnet build DsgWeb.slnx
dotnet test tests/DsgWeb.Tests/DsgWeb.Tests.csproj
```

The end-to-end tests require Docker; they start an `eclipse-mosquitto` container
via Testcontainers.
