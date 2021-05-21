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
            "Used":31185848.0,
            "Free":29576108.0
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

This is an example of how to use the metrics. As you can see, there are lots of opportunities to make this module even better!

Contributions are welcome. Issues are appreciated, pull requests are stellar!

The sourcecode is available [here](https://github.com/iot-edge-foundation/iot-edge-metrics/).

The Docker module is available [here](https://hub.docker.com/repository/docker/svelde/iot-edge-metrics)

## Blog

More details about how to use this blog are seen in this [blog](https://sandervandevelde.wordpress.com/2021/01/02/azure-iot-edge-module-metrics-in-action/).
