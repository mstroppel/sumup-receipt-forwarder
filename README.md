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

| Parameter          | Description                              | Default  |
|--------------------|------------------------------------------|----------|
| `WORKER_DELAY`     | Polling interval in minutes              | `15`     |
| `SUMUP_ACCOUNT_ID` | SumUp merchant account ID                | required |
| `SUMUP_API_KEY`    | SumUp API key for authentication         | required |
| `SMTP_HOST`        | SMTP server hostname                     | required |
| `SMTP_PORT`        | SMTP server port                         | `587`    |
| `SMTP_USERNAME`    | SMTP authentication username             | required |
| `SMTP_PASSWORD`    | SMTP authentication password             | required |
| `SMTP_USE_TLS`     | Enable TLS for SMTP connection           | `true`   |
| `SENDER_EMAIL`     | Email address used as the sender         | required |
| `RECIPIENT_EMAIL`  | Destination email for receipt forwarding | required |

## Env File

```dotenv
WORKER_DELAY=3600
SUMUP_ACCOUNT_ID=your-account-id
SUMUP_API_KEY=your-api-key
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USERNAME=your-username
SMTP_PASSWORD=your-password
SMTP_USE_TLS=true
SENDER_EMAIL=sender@example.com
RECIPIENT_EMAIL=recipient@example.com
```

## Implementation Details

- The worker stores already-sent receipt IDs locally so receipts are not forwarded twice.
- Logging is done to the console in JSON format so issues can be seen in container log aggregators (e.g. Dozzle can
  create alerts).
- The worker uses the official SumUp .NET SDK for API communication and `IHttpClientFactory` for receipt PDF downloads.
- Receipts are downloaded as PDF attachments and sent via SMTP using MailKit.

## Troubleshooting

Ensure the UID 1654 has write access to `/app/data` when mounted to the host file system.
