# TempSense

This program is meant to run on a Raspberry Pi, running Windows 10 IoT Core. Currently, it takes in temperature and humidity readings from a DHT-11 sensor and posts them on an MQTT topic. 

Add the IP address of the MQTT server and the name of the topic to post on into the `secrets.resw` file.

If you choose to write directly to an InfluxDB database, add your credentials in `secrets.resw` and uncomment the code.