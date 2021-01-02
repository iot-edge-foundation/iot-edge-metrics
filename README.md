# iot-edge-metrics
Parsing edgeHub and edgeAgent Prometheus metrics and turns it into JSON 


## Example output

The following output message is sent:

```
{
    "Timestamp":"2021-01-02T14:10:49.7778998Z",
    "Uptime":68,
    "Disks":{
        "sda2":{
            "Size":60761956.0,
            "Used":31185848.0,"Free":29576108.0
        },
        "sda1":{
            "Size":523248.0,
            "Used":6268.0,
            "Free":516980.0
        }
    },
    "Modules":{
        "hb":{
            "Cpu099":0.02,
            "MessagesSentCount":210,
            "MessagesRecievedCount":210
        },
        "edgeAgent":{
            "Cpu099":0.16,
            "MessagesSentCount":0,
            "MessagesRecievedCount":0
        },
        "host":{
            "Cpu099":1.06,
            "MessagesSentCount":0,
            "MessagesRecievedCount":0
        },
        "metrics":{
            "Cpu099":0.04,
            "MessagesSentCount":0,
            "MessagesRecievedCount":0
        },
        "edgeHub":{
            "Cpu099":0.12,
            "MessagesSentCount":0,
            "MessagesRecievedCount":0
        }
    },
    "MessagesSentCount":210,
    "MessagesRecievedCount":210
}
```

* Uptime is in minutes

## Module twin properties

The following desired properties are exposed:

* edgeHubMetricsVisible (default: false)
* edgeAgentMetricsVisible (default: false)
* enableSendMessages (default: false)
* interval; in seconds (default: 10000)

The latest property changes are reported back in the reported properties.

## Contributions

Contributions are welcome. Issues are appriciated, pull requests are stellar!