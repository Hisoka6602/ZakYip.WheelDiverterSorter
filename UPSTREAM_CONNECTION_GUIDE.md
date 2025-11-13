# Upstream Connection Configuration Guide

## Overview
All upstream connections (to RuleEngine) support both client and server modes with configurable reconnection strategies.

## Connection Modes

### 1. Client Mode
The WheelDiverterSorter actively connects to an upstream RuleEngine server.

**Features**:
- Automatic reconnection with exponential backoff
- Maximum backoff time: 5 seconds (configurable via `MaxBackoffSeconds`)
- Infinite retry attempts
- Suitable for: Connecting to centralized RuleEngine server

**Configuration Example**:
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Client",
    "TcpServer": "ruleengine.example.com:5000",
    "EnableAutoReconnect": true,
    "MaxBackoffSeconds": 5,
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  }
}
```

### 2. Server Mode
The WheelDiverterSorter listens for incoming connections from RuleEngine.

**Features**:
- Listens on configured port
- Accepts incoming connections
- No reconnection logic needed (server waits for clients)
- Suitable for: Distributed architectures where RuleEngine connects to multiple sorters

**Configuration Example**:
```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Server",
    "TcpServer": "0.0.0.0:5000",
    "TimeoutMs": 5000
  }
}
```

## Hot Reload Support

Configuration changes can be applied without restarting the application.

### Implementation
The application uses `IOptionsMonitor<RuleEngineConnectionOptions>` to detect configuration changes at runtime.

### Supported Changes
- Connection mode switching (Client ↔ Server)
- Server address changes
- Timeout and retry parameters
- Communication protocol changes (TCP, SignalR, MQTT, HTTP)
- Protocol-specific options (buffer sizes, keep-alive settings, etc.)

### How to Hot Reload

#### Option 1: Update appsettings.json
1. Edit `appsettings.json` or `appsettings.{Environment}.json`
2. Save the file
3. The application automatically detects and applies changes
4. Existing connections are gracefully closed and re-established with new settings

#### Option 2: Configuration API (if enabled)
Use the Configuration API to update settings programmatically:
```http
PUT /api/configuration/ruleengine
Content-Type: application/json

{
  "ConnectionMode": "Server",
  "TcpServer": "0.0.0.0:6000"
}
```

## Connection Modes by Communication Protocol

### TCP Socket
- **Client Mode**: Connect to TCP server at `TcpServer` address
- **Server Mode**: Listen on TCP port specified in `TcpServer`
- **Reconnection**: Exponential backoff up to `MaxBackoffSeconds`

### SignalR
- **Client Mode**: Connect to SignalR Hub URL
- **Server Mode**: Host SignalR Hub (requires additional configuration)
- **Reconnection**: Built-in SignalR reconnection with custom intervals

### MQTT
- **Client Mode**: Connect to MQTT Broker
- **Server Mode**: Not applicable (MQTT requires a broker)
- **Reconnection**: MQTT client library handles reconnection

### HTTP (Testing Only)
- **Client Mode**: Make HTTP requests to API endpoint
- **Server Mode**: Not applicable (pull model only)
- **Reconnection**: Retry on each request

## Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Mode` | CommunicationMode | Http | Protocol: Tcp, SignalR, Mqtt, Http |
| `ConnectionMode` | ConnectionMode | Client | Client or Server mode |
| `EnableAutoReconnect` | bool | true | Enable automatic reconnection (Client mode) |
| `MaxBackoffSeconds` | int | 5 | Maximum backoff time between reconnection attempts |
| `TimeoutMs` | int | 5000 | Connection/request timeout |
| `RetryCount` | int | 3 | Number of retries per operation |
| `RetryDelayMs` | int | 1000 | Initial delay between retries |
| `ChuteAssignmentTimeoutMs` | int | 10000 | Max wait time for chute assignment |

## Protocol-Specific Configuration

### TCP Options
```json
{
  "Tcp": {
    "ReceiveBufferSize": 8192,
    "SendBufferSize": 8192,
    "NoDelay": true,
    "KeepAliveInterval": 60
  }
}
```

### MQTT Options
```json
{
  "Mqtt": {
    "QualityOfServiceLevel": 1,
    "CleanSession": true,
    "SessionExpiryInterval": 3600,
    "MessageExpiryInterval": 0,
    "ClientIdPrefix": "WheelDiverter"
  }
}
```

### SignalR Options
```json
{
  "SignalR": {
    "HandshakeTimeout": 15,
    "KeepAliveInterval": 30,
    "ServerTimeout": 60,
    "ReconnectIntervals": [0, 2000, 5000, 10000],
    "SkipNegotiation": false
  }
}
```

### HTTP Options
```json
{
  "Http": {
    "MaxConnectionsPerServer": 10,
    "PooledConnectionIdleTimeout": 60,
    "PooledConnectionLifetime": 0,
    "UseHttp2": false
  }
}
```

## Reconnection Strategy (Client Mode)

The reconnection logic follows an exponential backoff strategy:

1. **Initial Attempt**: Immediate connection attempt
2. **First Retry**: Wait 1 second
3. **Second Retry**: Wait 2 seconds
4. **Third Retry**: Wait 4 seconds
5. **Fourth+ Retry**: Wait `MaxBackoffSeconds` (default 5 seconds)
6. **Continue**: Retry indefinitely with max backoff time

### Reconnection Triggers
- Initial connection failure
- Network disconnection
- Server timeout
- Read/write errors
- Manual disconnect followed by reconnect

## Example: Full Configuration

```json
{
  "RuleEngineConnection": {
    "Mode": "Tcp",
    "ConnectionMode": "Client",
    "TcpServer": "ruleengine.example.com:5000",
    "EnableAutoReconnect": true,
    "MaxBackoffSeconds": 5,
    "TimeoutMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "ChuteAssignmentTimeoutMs": 10000,
    "Tcp": {
      "ReceiveBufferSize": 8192,
      "SendBufferSize": 8192,
      "NoDelay": true,
      "KeepAliveInterval": 60
    }
  }
}
```

## Implementation Status

### ✅ Completed
- ConnectionMode enum (Client/Server)
- MaxBackoffSeconds configuration property
- Configuration model with hot reload support (IOptionsMonitor)
- Separate configuration files per protocol
- Documentation

### 🔄 To Be Implemented
- Actual reconnection logic with exponential backoff in TCP client
- Server mode implementation for TCP
- Server mode implementation for SignalR
- Connection state monitoring and event notification
- Metrics and logging for connection state changes

## Notes
- Server mode requires appropriate firewall and network configuration
- Client mode is recommended for most deployments
- Use Server mode when RuleEngine needs to connect to multiple distributed sorters
- Hot reload may cause brief connection interruptions
