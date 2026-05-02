
<div align="center">
    <p>
      <h1>Kornet src  v2</h1>
    </p>
</div>

this guide will be mostly a mix of the original v1, and some things i added.

 changed and site fixed by rytiufi and unknown and potato and syn etc+</a>
## v2 is just v1 but a little revamped (could have been revamped even more if kornet didnt shutdown)
## things you need

- <a href="https://nodejs.org/dist/v18.16.1/node-v18.16.1-x64.msi">Node.js</a>, *to run the renderer/build panel*
- <a href="https://sbp.enterprisedb.com/getfile.jsp?fileid=1258627">PostgreSQL</a>, *for the database*
- <a href="https://builds.dotnet.microsoft.com/dotnet/Sdk/6.0.412/dotnet-sdk-6.0.412-win-x64.exe">.NET 6.0</a>, *to run the website*
- <a href="https://go.dev/dl/go1.20.6.windows-amd64.msi">Go</a>, *for asset validation*
- <a href="https://www.python.org/ftp/python/3.12.8/python-3.12.8-amd64.exe">Python</a>, *for image validation, make sure to add to path in setup!*
- python packages: fastapi, aiohttp, pydub, uvicorn, python-magic, python-magic-bin==0.4.14, python-multipart, cryptography (pip install packagename to install, this also requires FFMPEG !)

u need php too i think for site proxy  (i didnt make it unknown did so credits to unknown)
## people who made this possible:
- rytiufi (me )
- potato
- syn  (made apk apis with me and unknown)
- unknown (made almost the whole src)
- floatzel (made ecs)
- harryzawg (made bubbablox which we used and bubba used ecs)
- bluedevyy
- washedbac (loser vibecoder u can check his code in v1)
- rccservice ( owner of bloxxia )
- yxga ( made launcher)
- aaron (cool guy fr fr )
## why did kornet shutdown???
my reputation is absolutely shit and me and unknown were fighting and he quit and from there potato and aaron lost hope on kornet and that lead to this  shutdown (potato and aaron and unknown are making a new rev named madoka 2019 revival gl to them ig )
## things that were planned before shutdown
2014 - half works but u gotta modify a lot before using it since it has hardcoded shit

2023 - studio exe is missing since it was 100 mb+ also studio worked but the local
rcc wont work for client cuz it was still w.i.p and 2023 client apis werent added yet

migration from 2016 render for assets to 2020 render - already done with avatars but not for assets like accessories etc only ones that render with 2020 are models and videos and animations  r6 and r15 avatars render with 2020! 3d render too
(if u dig enough through the commits u will find old tests of the 2020 migration for assets by directly using soap instead of the webserver ecs 2016 render )
## why are the subdomains and some parts of the frontend vibecoded and backend??
unknown was tired and he made some of the subdomains with ai since he was tryna sleep  and some parts of the frontend are vibecoded by washedbac who got fired right after 

## full guide soon  with launcher src setup and etc 
here is db (ill soon give schema instead of full db and actually working setup guide soon ): https://drive.google.com/file/d/1y1LIa0_TK8Mhpo8YUcFiJg0WqDE4riPt/view?usp=sharing
also launcher is in repo already 
