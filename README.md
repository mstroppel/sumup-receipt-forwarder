# sumup-receipt-forwarder

SumUp Receipt Forwarder is a lightweight .NET worker service running inside a Docker container. It automatically retrieves daily transaction data from a connected SumUp account, downloads each transaction’s PDF receipt, and forwards them via SMTP to a specified email address.

Features and Configuration:

- SumUp account credentials: Used for authentication and API access.
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
      - CONFIG_ONE="value of config one"

```

Parameters:

- CONFIG_ONE: config parameter
