## CoderDojo Workshop Planer

![Deploy CDW-Planner to Azure Function App](https://github.com/coderdojo-linz/coderdojo-workshop-planner/workflows/Deploy%20CDW-Planner%20to%20Azure%20Function%20App/badge.svg)

### How to construct a correct workshop plan

#### Create a Folder

+ Folder format must be `YYYY-MM-DD` like `2020-07-16`
+ Create for every date a new folder

#### Create the YML File

+ File must be named `PLAN.yml` or `plan.yml`.
+ It must be a `.yml` or `.yaml` file
+ Insert your workshop data in that file. <br><b>Do not create a file for every single workshop!</b>

#### Syntax

```yml
workshops:
- begintime: 'your time'
  endtime: 'your time'
  status: Draft/Published/Scheduled
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

| Parameter     | Description |
| ------------  | ----------- |
|begintime      |`00:00` e.g 13:45|
|endtime        |`00:00` e.g 15:45|
|status         | If the workshop is not fixed, set `Draft`, if you want to publish it to, but don't want the Zomm meetings yet, set `Published`, if everythings correct and you want to create Zoom meetings, set `Scheduled`|
|title          | e.g Scratch|
|targetAudience | e.g kids above 6 years|
|description    | describe your workshop in some sentences|
|prerequisites  | If they need to install software|
|mentors        | Probably you|

These following parameter are needed to be created in the Azure function

| Setting Parameter        | Description |
| ------------             | ----------- |
|`MONGOUSER`               |Your username, you need to have admin rights|
|`MONGOPASSWORD`           |Your password|
|`MONGODB`                 |The database name e.g. `member-management-test`|
|`MONGOCONNECTION`         |The collection name e.g. `events`|
|`MONGOCOLLECTIONEVENTS`   |A connection string, where your workshops are, for more details check the link down below|
|`MONGOCOLLECTIONMENTORS`  |A connection string, where your mentors are|
|`ServiceBusConnection`    |Your have to create a Service-Bus-Connection to communicate with certain functions, for more details check the link down below |
|`GITHUBUSER`|Name of the repository where you create the yaml files e.g. `coderdojo-linz/coderdojo-online`|
|`ZOOMTOKEN`|Token is required to connect our software with Zoom|
|`EMAILAPIKEY`|ApiKey for the email connection|
|`EMAILSENDER`|Name of the email sender e.g. `info@linz.coderdojo.net`|

#### Useful Links
+ Connection-Strings: https://docs.mlab.com/connecting/#connect-string
+ Service-Bus-Connection: https://docs.tibco.com/pub/flogo-azservicebus/1.0.0/doc/html/GUID-04B0556E-B623-492E-9531-1A6ECA64284F.html
+ Zoom-Token: https://marketplace.zoom.us/docs/guides/auth/oauth#getting-access-token


