# sumup-receipt-forwarder

SumUp Receipt Forwarder is a lightweight .NET worker service running inside a Docker container.
It automatically retrieves transaction data from a connected SumUp account, downloads each transaction’s PDF receipt,
and forwards them via SMTP to a specified email address.

Features and Configuration:

- SumUp account:
  - account id
  - API credentials
- Execution interval: Defines how often the worker runs to check for new transactions.
- SMTP settings: Configures the mail server used to send receipts.
- Target email address: Destination address where receipts are delivered.

SumUp receipt URL

https://sales-receipt.sumup.com/pos/public/v1/{account-id}/receipt/{receipt-guid}?format=pdf

## Usage

```yaml
services:
  sumup-receipt-forwarder:
    image: ghcr.io/mstroppel/sumup-receipt-forwarder:latest
    restart: unless-stopped
    environment:
      - WORKER_DELAY=3600
      - SUMUP_ACCOUNT_ID=your-account-id
      - SUMUP_API_KEY=your-api-key
      - SMTP_HOST=smtp.example.com
      - SMTP_PORT=587
      - SMTP_USERNAME=your-username
      - SMTP_PASSWORD=your-password
      - SMTP_USE_TLS=true
      - SENDER_EMAIL=sender@example.com
      - RECIPIENT_EMAIL=recipient@example.com
    volumes:
      - receipt-data:/app/data

volumes:
  receipt-data:
```

Parameters:

| Parameter | Description | Default |
|---|---|---|
| `WORKER_DELAY` | Polling interval in minutes | `15` |
| `SUMUP_ACCOUNT_ID` | SumUp merchant account ID | required |
| `SUMUP_API_KEY` | SumUp API key for authentication | required |
| `SMTP_HOST` | SMTP server hostname | required |
| `SMTP_PORT` | SMTP server port | `587` |
| `SMTP_USERNAME` | SMTP authentication username | required |
| `SMTP_PASSWORD` | SMTP authentication password | required |
| `SMTP_USE_TLS` | Enable TLS for SMTP connection | `true` |
| `SENDER_EMAIL` | Email address used as the sender | required |
| `RECIPIENT_EMAIL` | Destination email for receipt forwarding | required |

## Implementation Details

- The worker stores already-sent receipt IDs locally so receipts are not forwarded twice.
- Logging is done to the console in JSON format so issues can be seen in container log aggregators (e.g. Dozzle can create alerts).
- The worker uses the official SumUp .NET SDK for API communication and `IHttpClientFactory` for receipt PDF downloads.
- Receipts are downloaded as PDF attachments and sent via SMTP using MailKit.

## Implementation Plan

### Phase 1: Configuration and Settings

- [x] Extend `SumUpReceiptForwarderSettings` with all required properties (SumUp account ID, API key, SMTP host/port/username/password/TLS, sender email, recipient email).
- [x] Update `Program.cs` to read all new environment variables and register the settings.
- [x] Update `Dockerfile` and `docker-compose.yml` with the new environment variables.

### Phase 2: SumUp API Client

- [x] Add `HttpClient` / `IHttpClientFactory` registration in `Program.cs`.
- [x] Create `ISumUpApiClient` interface and `SumUpApiClient` implementation.
- [x] Implement authentication against the SumUp API (API key / OAuth2).
- [x] Implement transaction listing (fetch recent transactions for the configured account).
- [x] Implement receipt PDF download from `https://sales-receipt.sumup.com/pos/public/v1/{account-id}/receipt/{receipt-guid}?format=pdf`.
- [x] Add response models / DTOs for SumUp API responses.

### Phase 3: Email Forwarding Service

- [x] Add MailKit NuGet dependency.
- [x] Create `IEmailService` interface and `EmailService` implementation.
- [x] Implement SMTP connection with TLS support.
- [x] Implement sending emails with PDF receipt attachments.

### Phase 4: Receipt Tracking (Deduplication)

- [x] Create `IReceiptTracker` interface and `FileReceiptTracker` implementation.
- [x] Implement file-based persistence of already-forwarded receipt IDs (JSON or plain text in a Docker volume).
- [x] Add methods: `IsAlreadySent(string receiptId)` and `MarkAsSent(string receiptId)`.

### Phase 5: Worker Integration

- [x] Wire all services into the DI container in `Program.cs`.
- [x] Implement the main worker loop in `SumUpReceiptForwarderWorker.ExecuteAsync`:
  1. Fetch recent transactions from SumUp API.
  2. Filter out already-forwarded receipts.
  3. Download PDF for each new receipt.
  4. Send email with PDF attachment.
  5. Mark receipt as sent.
- [x] Add error handling and retry logic (per-receipt failures should not stop the batch).
- [x] Add structured logging for each step (transaction count, receipts forwarded, errors).

### Phase 6: Testing

- [x] Rename `CalendarSyncWorkerTests.cs` to `SumUpReceiptForwarderWorkerTests.cs`.
- [ ] Write unit tests for `SumUpApiClient` (mocked HTTP responses).
- [ ] Write unit tests for `EmailService` (mocked SMTP).
- [x] Write unit tests for `FileReceiptTracker`.
- [x] Write integration tests for the worker loop logic.

### Phase 7: Cleanup

- [x] Remove the `template/` directory and `nupkg/` folder (leftover scaffolding artifacts).
- [x] Review `.gitignore` for completeness.

