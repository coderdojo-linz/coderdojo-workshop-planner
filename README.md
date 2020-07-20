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

