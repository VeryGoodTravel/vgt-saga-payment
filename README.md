# vgt-saga-payment

Main repository of the payment service.

Handles up to 10 concurrent payments and gives true/false answers randomly in random time.

## Repository

This repository contains additional submodules containing shared libraries of the SAGA microservices implementations.

To update those submodules in the local branch run:

    git submodule update --remote --merge

## Configuration

### Environmental variables

- RABBIT_HOST -> Address of the rabbit server.
- RABBIT_VIRT_HOST -> Virtual host of the rabbit server.
- RABBIT_PORT -> Port of the rabbit server.
- RABBIT_USR -> Username to log in with.
- RABBIT_PASSWORD -> User password to log in with.
- RABBIT_REPLIES -> Queue of the replies sent back to the orchestrator.
- RABBIT_PAYMENT -> Queue of the requests sent by the orchestrator to the payment gate service.
- PAYMENT_MIN_DELAY -> Minimum value (in seconds) random delay uses, defaults to 0
- PAYMENT_MAX_DELAY -> Maximum value (in seconds) random delay uses, defaults to 100

## Implementation documentation
XML docs of the project available in the repository in the
file [SagaPaymentsDocumentation.xml](SagaPaymentsDocumentation.xml)
