## CoderDojo Workshop Planer

### How to construct a correct workshop plan
#### Create a Folder
+ Folder format must be `YYYY-MM-DD` like `2020-07-16`
+ Create for every date a new folder

#### Create the YML File
+ File must be named `PLAN.yml` or `plan.yml`.
+ It must be a `.yml` or `.yaml` file
+ Insert your workshop data in that file. <br><b>Do not create a file for every single workshop!</b>

#### Syntax:
```yml
workshops:
- begintime: 'your time'
  endtime: 'your time'
  draft: true/false
  title: yourtitle
  targetAudience: 'your audience'
  description: >- 
    your describtion
  prerequisites: >-
    - your prerequisites + links
  mentors:
    - you
  zoom: 'zoomlink'
```


| Parameter                | Description |
| ------------             | ----------- |
|begintime|`YYYY-MM-TTT00:00:00` e.g 2020-07-03T12:45:00|
|endtime|`YYYY-MM-TTT00:00:00`|
|draft| If the workshop is not fixed, set true|
|title| e.g Scratch|
|targetAudience| e.g kids above 6 years|
|description| describe your workshop in some sentences|
|prerequisites| If they need to install software|
|mentors| Probably you|
|zoom| Zoom - link|


These following parameter are needed to be created in the Azure function

| Setting Parameter                | Description |
| ------------             | ----------- |
|`MONGOUSER`|Your username, you need to have admin rights|
|`MONGOPASSWORD`|Your password|
|`MONGODB`|The database name e.g. `member-management-test`|
|`MONGOCONNECTION`|The collection name e.g. `events`|
|`MONGOCOLLECTION`|A connection string, more details: https://docs.mlab.com/connecting/#connect-string|
|`ServiceBusConnection`|Your have to create a Service-Bus-Connection to communicate with certain functions, more details: https://docs.tibco.com/pub/flogo-azservicebus/1.0.0/doc/html/GUID-04B0556E-B623-492E-9531-1A6ECA64284F.html|


