# vgt-saga-orders

Main repository of the saga process.
Contains both orders microservice and orchestrator

## Configuration

### Environmental variables

- RABBIT_HOST -> Address of the rabbit server.
- RABBIT_VIRT_HOST -> Virtual host of the rabbit server.
- RABBIT_PORT -> Port of the rabbit server.
- RABBIT_USR -> Username to login with.
- RABBIT_PASSWORD -> User password to login with.
- RABBIT_REPLIES -> Queue of the replies sent back to the orchestrator.
- RABBIT_ORDER -> Queue of the requests sent by the orchestrator to the order service.
- RABBIT_PAYMENT -> Queue of the requests sent by the orchestrator to the payment gate service.
- RABBIT_HOTEL -> Queue of the requests sent by the orchestrator to the hotel service.
- RABBIT_FLIGHT -> Queue of the requests sent by the orchestrator to the flight service.

## Implementation documentation
XML docs of the project available in the repository in the
file [SagaOrdersDocumentation.xml](SagaOrdersDocumentation.xml)