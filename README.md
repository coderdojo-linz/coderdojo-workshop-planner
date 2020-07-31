# CoderDojo Workshop Planer

![Deploy CDW-Planner to Azure Function App](https://github.com/coderdojo-linz/coderdojo-workshop-planner/workflows/Deploy%20CDW-Planner%20to%20Azure%20Function%20App/badge.svg)

> ## Welcome information

*Note: The project and descriptions are in english, but the CoderDojo is in Austria -> german language. So the messages are all german.*

Hello person, who is slightly interested in programming. This is our `CoderDojo Workshop Planer`.
What? You don't know what that is? You don't even know what the `CoderDojo` is?

Well here's a short summary about the CoderDojo and this project.

> ### What's the CoderDojo?

The CoderDojo is a programming club for kids and teens between 6 - 17 years in Linz/ Leonding.
We meet every two weeks for our `regular CoderDojo` and every other week for a `Playground.`
You can work on a project yourself or with our mentors or friends.
You don't have the slightest idea about programming? 
Well don't worry, our mentors will help you and beginners are going to start with Scratch (if you want).

> ### What's the Online CoderDojo?

Since quarantine, we weren't able to do our usual CoderDojo meetings, so we changed it to `online meetings` every week.

> ### How were the meetings organized before the Workshop Planer?

Before this glorious project, all the meetings had to be organized manually. 
It was a lot of work, because to create workshops, the admin/ mentors had to follow these steps:

+ Workshop info were written in a markdown in "date folder" (more info below)
+ These info were manually transfered to a MongoDB database
+ Zoom meetings had to be created
+ The info had to be displayed at the CoderDojo website
  
If one mentor wanted to change something about their workshop, all these steps had to be repeated.
It costed a lot of hard work and nerves.

> ### What does the CoderDojo Workshop Planer do?

So, our program automates these steps above with some nice features.

> ## Functionality

*Explanation down below*

![Diagram](https://github.com/coderdojo-linz/coderdojo-workshop-planner/blob/master/doc/cdwplanner_diagram.png)

> ### Get the data

*Note: We seperate the program with the input file, so in this repository is our program, which is deployed to Azure via a `GitHub Action` on a `push` request, heres the [link](https://github.com/coderdojo-linz/coderdojo-online) for the other repository*

+ Basically you create a date folder `YYYY-MM-DD`
+ In the folder is a `yml` file with all the data we need for processing the workshop
+ The data is sent via GitHub `Webhook` to our Azure function (the program) on a push request
+ We get the body and transform the `Json into C#` 
+ This data is sent to a `ServiceBus Topic`
+ The data is written into a `MongoDB database` via a subsciption method of the topic

Now we have the data in the database. Now what? There are a some uses for them.

> ### Displaying the data on the website

From the database the data is displayed on the workshop schedule on the CoderDojo website.
This is done with the method `events`.
The website send a HTTP Trigger to this function, which sends the data back.
For only listing future meetings, there is a parameter in the URL called `past`. It can be set `true` or `false`

> ### Creating Zoom meetings

If you set the `status` flag in the yml file to `Scheduled`, Zoom meetings are going to be created or updated.
How is that done?

Well, we have 4 Zoom user available, so we send a `GET` request to Zoom to get our users.
Then we check if the meeting already exists, so that we won't have it double.

Then we send a `PATCH` if it already exists and a `POST` request if not.
How do we check if it already exists?

If the yml file we have a `shortCode` flag, where we write a word, with specifies that workshop.
In Zoom we write that flag in the describtion, so that we can find it later.

After we created/updated a meeting we write/update the attendance URL and the Zoom user the into our database.

> ### Getting a newsletter template

For every upcoming event, the CoderDojo admin sends a newsletter where the workshops are listed.
To automate this task too, there is a nice feature.

Basically the program reads the workshops from the database with a specific date filter from the url and transforms this into a html template, which it sends back and displays it.
The admin uses that template to transforms it via `MailChimp` into a newsletter.

> ### Sending emails to mentors

Our mentors get emails about their workshops. In those emails is the basic info listed like

+ Title
+ Begintime
+ Endtime
+ Description
+ Zoom user
+ Zoom URL
+ Hostkey

The hostkey is for hosting the meeting, so we only send the mail to the first listed mentor. Why? Because there'll b e a conflict, if there are two hosts in a Zoom meeting.
We also send a `ics` file with the mail. (A calender entry), that we also build with the workshop data.

To send the email, we use `SendGrid`.
As URL parameter there's the date again.

To get the mentor's info, we created an extra collection in the database with the mentor's 

+ nickname
+ firstname
+ lastname
+ email

> ### Discord-Bot

The CoderDojo also has a Discord server where all info for workshops are being posted. There are also channels, where you can communicate with mentors or other coders.

So, we implemented a Discord bot, which sends a specific message each time, the workshop is being updated.

Sample:
![NewWorkshop](https://github.com/coderdojo-linz/coderdojo-workshop-planner/blob/master/doc/botCreate.png)
![NewTitle](https://github.com/coderdojo-linz/coderdojo-workshop-planner/blob/master/doc/botUpdateTitle.png)
![NewBegintime](https://github.com/coderdojo-linz/coderdojo-workshop-planner/blob/master/doc/botUpdateTime.png)

> ## How to construct a correct workshop plan

> ### Create a folder

+ Folder format must be `YYYY-MM-DD` like `2020-07-16`
+ Create for every date a new folder

> ### Create the YML file

+ File must be named `PLAN.yml` or `plan.yml`.
+ It must be a `.yml` or `.yaml` file.
+ Insert your workshop data in that file. <br>**Do not create a file for every single workshop! Only one big file!**

> ### Syntax

```yml
workshops:
- begintime: your time
  endtime: your time
  status: Draft/Published/Scheduled
  title: yourtitle
  targetAudience: your audience
  description: |
    your describtion
  prerequisites: |
    - your prerequisites + links
  mentors:
    - you
  shortcode: your-shortcode
```

> ### Parameter

| Parameter     | Description                                                                                |
| ------------  | -----------                                                                                |
|begintime      | Syntax: `00:00` e.g 13:45                                                                  |
|endtime        | Syntax: `00:00` e.g 15:45                                                                   |
|status         | If the workshop is not fixed, set `Draft`, if you only want send it to the database, but don't want the Zoom meetings yet, set `Published`, if everythings correct and you want to create Zoom meetings, set `Scheduled`                                                                                    |
|title          | e.g Scratch                                                                                |
|targetAudience | e.g kids above 6 years                                                                     |
|description    | Describe your workshop in some sentences                                                   |
|prerequisites  | If they need to install software                                                           |
|mentors        | Probably you *Note: It's an array, so there can be more mentors in a workshop*             |
|shortCode      | A word that describes your workshop e.g `Elektronikbasteln`. *Note: Your are `not` allowed to change the shortCode once you commited the file*                                                          |


> ### That's how your mentors collection need to look like

*Note: Create for each mentor a new document*

```json
{
    "nickname": "Someone's nickname",
    "email": "someone@something.at",
    "firstname": "Someone's firstname",
    "lastname": "Someone's lastname"
}
```

> ### These following parameter are needed to be created in the Azure function

| Setting Parameter        | Description                                                                    |
| ------------             | -----------                                                                    |
|`MONGOUSER`               |Your username, you need to have admin rights                                    |
|`MONGOPASSWORD`           |Your password                                                                   |
|`MONGODB`                 |The database name e.g. `member-management-test`                                 |
|`MONGOCONNECTION`         |A connection string, where your workshops are, for more details check the link down below                                                                                                  |
|`MONGOCOLLECTIONMEVENTS`  |The collection name of your workshop collection e.g. `events`                   |
|`MONGOCOLLECTIONMENTORS`  |The collection name of your mentors collection e.g. `mentor-info`               |
|`ServiceBusConnection`    |Your have to create a Service-Bus-Connection to communicate with certain functions, for more details check the link down below                                                       |
|`GITHUBUSER`              |Name of the repository where you create the yaml files e.g. `coderdojo-linz/coderdojo-online`                                                                                           |
|`ZOOMTOKEN`               |Token is required to connect our software with Zoom                             |
|`EMAILAPIKEY`             |ApiKey for the email connection                                                 |
|`EMAILSENDER`             |Name of the email sender e.g. `info@linz.coderdojo.net`                         |

> ### Useful links

+ [CoderDojo](https://coderdojo-linz.github.io/)
+ [CoderDojo-Meetings](https://linz.coderdojo.net/termine/)
+ [MailChimp](https://mailchimp.com/)
+ [SendGrid](https://sendgrid.com/marketing/sendgrid-services-cro/?extProvId=5&extPu=49397-gaw&extLi=164417502&sem_adg=8807285742&extCr=8807285742-321630592511&extSi=&extTg=&keyword=%2Bsendgrid&extAP=&extMT=b&utm_medium=cpc&utm_source=google&gclid=CjwKCAjw34n5BRA9EiwA2u9k3zDFCGr34XlStDoPazJflgeouA9gi3apBJt6A5AeXLUvNqiSqGY6ahoCGgIQAvD_BwE)
+ [Connection-Strings](https://docs.mlab.com/connecting/#connect-string)
+ [Service-Bus-Connection](https://docs.tibco.com/pub/flogo-azservicebus/1.0.0/doc/html/GUID-04B0556E-B623-492E-9531-1A6ECA64284F.html)
+ [Zoom-Token](https://marketplace.zoom.us/docs/guides/auth/oauth#getting-access-token)
