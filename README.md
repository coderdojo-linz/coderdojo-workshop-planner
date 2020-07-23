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


| Parameter     | Description |
| ------------  | ----------- |
|begintime      |`00:00` e.g 13:45|
|endtime        |`00:00` e.g 15:45|
|draft          | If the workshop is not fixed, set true|
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
|`MONGOCOLLECTION`          |A connection string, more details: https://docs.mlab.com/connecting/#connect-string|
|`ServiceBusConnection`|Your have to create a Service-Bus-Connection to communicate with certain functions, more details: https://docs.tibco.com/pub/flogo-azservicebus/1.0.0/doc/html/GUID-04B0556E-B623-492E-9531-1A6ECA64284F.html|
|`GITHUBUSER`|Name of the repository where you create the yaml files e.g. `coderdojo-linz/coderdojo-online`|


