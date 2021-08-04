# Getting Started with Data Analytics & Machine Learning in F#

This repo contains code shown in [.NET Conf - Focus on F# - Getting Started with Data Analytics & Machine Learning in F# session](https://www.youtube.com/watch?v=i3qEhwcG7ps)

## Scenario

Train and deploy a machine learning model to predict the score given to a restaurant after inspection using features such as the number of critical violations, violation type, and a few others.

## Prerequisites

- [.NET 5.0 SDK or later](http://dotnet.microsoft.com/download)
- [Configure .NET for Apache Spark](https://docs.microsoft.com/dotnet/spark/tutorials/get-started)
- Optional
  - [VS Code](https://code.visualstudio.com/download)
  - [Ionide extension](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp)
  - [.NET Interactive Notebooks extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode)
  - [Farmer](https://compositionalit.github.io/farmer/about/)

## Project Assets

Slides are in the [dotnetconf-fsharp-machine-learning.pdf](./dotnetconf-fsharp-machine-learning.pdf) file.

- **data** - Location for original dataset.
- **webapi** - F# Saturn Web API to host *InspectionModel.zip* regression model.
  - **Domain.fs** - Contains the schema of the model input and output.
  - **Program.fs** - Web API Entrypoint
  - **InspectionModel.zip** - Regression model to predict the score of a restaurant inspection
  - **Deploy.fsx** - Farmer deployment script.
- **data-prep-modeling.dib** - .NET Interactive Notebook to explore & visualize data, and train a machine learning model
- **data-prep-modeling.fsx** - F# Interactive script to explore & visualize data, and train a machine learning model (same code as *dib* notebook without interactive displays).
- **spark-debug.cmd** - Console script to start .NET for Apache Spark in Debug mode.

## About the data

The dataset represents the NYC restaurant inspection results. For more information, see [DOHMH New York City Restaurant Inspection Results](https://data.cityofnewyork.us/Health/DOHMH-New-York-City-Restaurant-Inspection-Results/43nn-pn8j).

### Get the data

1. Navigate to the [DOHMH New York City Restaurant Inspection Results](https://data.cityofnewyork.us/Health/DOHMH-New-York-City-Restaurant-Inspection-Results/43nn-pn8j) page.
1. Select **Export > CSV**.

    ![NYC data portal restaurant inspections dataset page](https://user-images.githubusercontent.com/46974588/128229956-adcffd6f-30f8-49ad-a0f3-eeec4bf9a073.png)

1. A csv file is downloaded to your PC.
1. Copy it to the *data* directory and rename it *nyc-restaurant-inspections.csv*.

## Run application

### Prepare data & train model

.NET for Apache Spark is used to prepare the raw data for training. To prep data, start Apache Spark.

1. Start Apache Spark in debug mode. **This sample uses Apache Spark 2.4. Depending on your Spark version you might have to reference a different jar from the *jars* directory in your *spark-debug* script.**

    1. Windows CMD Prompt

        ```console
        spark-debug.cmd
        ```

    1. Unix (Linux/Mac)

        ```bash
        spark-debug.sh        
        ```

**Make sure to keep the window you ran this script on open until *data-prep-modeling.fsx* is done running done preprocessing your data**.

2. Use .NET CLI and F# Interactive to run *data-prep-modeling.fsx*.

    ```dotnetcli
    dotnet fsi data-prep-modeling.fsx
    ```

This will prepare the data, create an output directory *processed-data* for the results, and save the results to *.csv* files.

Automated ML (AutoML) in ML.NET is used to train a regression model to predict the inspection score.  This script trains your model and saved the serialized version of it to a file called *InspectionModel.zip*.

**Remember to stop Apache Spark once *data-prep-modeling.fsx* is done preparing the data. `Ctrl + C`**

## Run Web API

Saturn is used to create an HTTP endpoint to make predictions with the *InspectionModel.zip* model. Use the .NET CLI to start the Web API

Navigate to the *webapi* directory and start your application

```dotnetcli
dotnet run
```

### Make predictions

Once the Web API starts, send an HTTP **POST** request to `http://localhost:5000/predict` with the following JSON body:

```json
{
    "Camis":"41720083",
    "Boro":"Manhattan",
    "InspectionType":"Cycle Inspection / Re-inspection",
    "InspectionScore": 11.0,
    "Violations":"04H,09C,10F",
    "CriticalViolations":1.0,
    "TotalViolations":3.0
}
```

If the request is handled successfully, you should get a response similar to the following:

```json
{
  "InspectionScore": 11,
  "Score": 11.56095
}
```

## Optional (Deploy)

The following commands assume you have the Azure CLI installed & are logged in to your account.

1. Navigate to the *webapi* directory.
1. Get your application ready for deployment

    ```dotnetcli
    dotnet publish -c Release -o deploy
    ```

    This command creates a directory called *deploy* containing all the files you'll need to deploy your application.

1. Update the name of your application and resource group in *Deploy.fsx*.
1. Run *Deploy.fsx* to deploy your application.

```dotnetcli
dotnet fsi Deploy.fsx
```

If your deployment is successful, you should see your application in the Azure Portal.

## Resources

- [F# Documentation](http://docs.microsoft.com/dotnet/fsharp)
- [.NET for Apache Spark Documentation](http://docs.microsoft.com/dotnet/spark)
- [ML.NET Documentation](http://docs.microsoft.com/dotnet/machine-learning)
- [ML.NET F# Samples](https://github.com/dotnet/machinelearning-samples/tree/main/samples/fsharp)
- [.NET Interactive](https://github.com/dotnet/interactive)
- [F# Learn Path](https://docs.microsoft.com/en-us/learn/paths/fsharp-first-steps/)
- [F# Beginner Video Series](https://aka.ms/BeginnersSeriesFSharp)