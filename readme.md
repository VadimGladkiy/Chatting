1. Required Docker installed on your machine.

2. For testing the project has a separate docker compose file called 'compose.images.yaml'.
Obtain the 'compose.images.yaml' from the project repository or from the project author.

3. Then you need to create an .env file in the same folder as 'compose.images.yaml'. 
The example of the .env file is provided in the project repository as '.env.example'.
In the .env file you need to set the following variables: 

Shortly the .env file has the next content:

# sets the variable for docker
DB_PASSWORD=ReplaceWithAStrongPassword123!

In that file you just need to replace the DB_PASSWORD value with your own strong password and save.

4. In the terminal navigate to a folder with 'compose.images.yaml' and '.env' files located. Run the following commands:

4.1 docker compose -f compose.images.yaml pull

4.2 docker compose -f compose.images.yaml up -d

5. Go to Docker -> Containers :

There must be chatting compose stack with 3 containers running: sql, app, client. 
In the client's row there should be a port to open the application. 
Clicking on that port will open the application in your browser.
