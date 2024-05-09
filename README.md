# RickAndMorty_Umbraco

#### Prerequisites
Umbraco 13.2.0: Ensure you have Umbraco 13.2.0 installed locally on your development machine.
SSMS: You will need SQL Server Management Studio to restore the project's database.

#### Setting Up the Database
Restore the Database: Locate the RickAndMorty_Umbraco.bacpac file within the project's Files folder.
Use SQL Server Management Studio: Launch SQL Server Management Studio and connect to your local SQL Server instance.
Import the Database: Within SSMS, navigate to the Import and Export Data wizard and follow the prompts to restore the RickAndMorty_Umbraco.bacpac file as the project's database.

#### Configuration
Update appsettings.json: Locate the appsettings.json file within the project root.
Database Connection String: Update the connection string within the ConnectionStrings section of the appsettings.json file to point to your local SQL Server instance and the database name you restored in the previous step.

#### Running the Project
Run the Application: Run the application using your IDE's debugging functionality.
or use the command line and run "dotnet run" from the project root. 
