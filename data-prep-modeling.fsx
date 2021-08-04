(*DATA PREPARATION*)

// Install & reference Microsoft.Spark package

open System.Threading

#r "nuget:Microsoft.Spark"

printfn "Loading Microsoft Spark package"
Thread.Sleep(15000)

open Microsoft.Spark.Sql

// Initialize Spark Session

let sparkSession = 
    SparkSession
        .Builder()
        .AppName("on-dotnet-fsharp")
        .GetOrCreate()

// Load data into Spark DataFrame

let df = 
    sparkSession
        .Read()
        .Option("header","true")
        .Option("inferSchema","true")
        .Csv("data/nyc-restaurant-inspections.csv")

df.PrintSchema()

// Group data by borough

let boroughs = 
    df.GroupBy([|Functions.Col("BORO")|]).Count()

boroughs.Show()

// Remove null or missing boroughs

let cleanBoroughs = boroughs.Filter(Functions.Col("BORO").NotEqual("0"))

cleanBoroughs.Show()

// Install & reference Plotly.NET visualization package

#r "nuget: Plotly.NET, 2.0.0-preview.5"
#r "nuget: Plotly.NET.Interactive, 2.0.0-preview.5"

open Plotly.NET

// Plot borough information

let boroughs,counts = 
    cleanBoroughs
      .Select("BORO","count")
      .OrderBy(Functions.Col("count").Desc())
      .Collect()
      |> Seq.map(fun row -> (string row.[0], string row.[1] |> int))
    |> Seq.unzip

let boroughColumn = Chart.Column(boroughs,counts)

boroughColumn

// Get coordinate information

let coordinates = 
  df
    .Select(
      Functions.Col("CAMIS"),Functions.Col("Latitude"),Functions.Col("Longitude"))
    .DropDuplicates("CAMIS")

coordinates.Show()

// Remove null or missing coordinates

let nonZeroCoordinates = coordinates.Where("Latitude != 0.0 OR Longitude != 0.0")

// Plot coordinates

let labels, lat, lon = 
    nonZeroCoordinates.Select("CAMIS","Latitude","Longitude").Collect()
    |> Seq.map(fun row -> string row.[0], string row.[1] |> float, string row.[2] |> float)
    |> Seq.unzip3

let pointMapbox = 
    Chart.PointMapbox(
        lon,lat,
        Labels = labels,
        TextPosition = StyleParam.TextPosition.TopCenter
    )
    |> Chart.withMapbox(
        Mapbox.init(
            Style=StyleParam.MapboxStyle.OpenStreetMap,
            Center=(-73.99,40.73),
            Zoom=8.
        )
    )

pointMapbox

(*
Query & prepare data for modeling

In this query, the dataset is processed to extract the following columns:

- CAMIS
- BORO
- INSPECTION DATA
- INSPECTION TYPE
- VIOLATION CODE
- CRITICAL FLAG
- SCORE

SCORE is the column to predict when we train our machine learning model. The rest of the columns will be the features or inputs for our model.    
*)

let prepData = 
    df
      .Select("CAMIS","BORO","INSPECTION DATE","INSPECTION TYPE","VIOLATION CODE","CRITICAL FLAG","SCORE")
      .Where(Functions.Col("INSPECTION DATE").NotEqual("01/01/1900"))
      .Where(Functions.Col("SCORE").IsNotNull())
      .Where(Functions.Col("BORO").NotEqual("0"))
      .WithColumn("CRITICAL FLAG",Functions.When(Functions.Col("CRITICAL FLAG").EqualTo("Critical"),1).Otherwise(0))
      .GroupBy("CAMIS","BORO","INSPECTION DATE","INSPECTION TYPE", "SCORE") //"INSPECTION TYPE","VIOLATION CODE"
      .Agg(
          Functions.CollectList(Functions.Col("CRITICAL FLAG")).Alias("VIOLATIONS"),
          Functions.CollectList(Functions.Col("VIOLATION CODE")).Alias("CODES"))
      .WithColumn("CRITICAL VIOLATIONS", Functions.Expr("AGGREGATE(VIOLATIONS, 0, (acc, val) -> acc + val)"))
      .WithColumn("TOTAL VIOLATIONS", Functions.Size(Functions.Col("VIOLATIONS")))
      .WithColumn("CODES",Functions.ArrayJoin(Functions.Col("CODES"),","))
      .Drop("VIOLATIONS","INSPECTION DATE")
      .OrderBy("CAMIS")

// Save data
// The results of the query are saved to a directory called *processed-data*

prepData.Write().Mode(SaveMode.Overwrite).Csv("processed-data")

(*TRAIN THE MODEL*)
// In this section, we'll take the data that was processed using .NET for Apache Spark and use it for training

// Install and reference ML.NET & AutoML packages

#r "nuget:Microsoft.ML"
#r "nuget:Microsoft.ML.AutoML"

open System.IO
open Microsoft.ML
open Microsoft.ML.Data
open Microsoft.ML.AutoML

// Define model input and output schema

[<CLIMutable>]
type ModelInput = {
    [<LoadColumn(0)>] Camis: string
    [<LoadColumn(1)>] Boro: string
    [<LoadColumn(2)>] InspectionType: string
    [<LoadColumn(3)>] InspectionScore: float32
    [<LoadColumn(4)>] Violations: string
    [<LoadColumn(5)>] CriticalViolations: float32
    [<LoadColumn(6)>] TotalViolations: float32
}

[<CLIMutable>]
type ModelOutput = {
    InspectionScore: float32
    Score: float32
}

// Initialize MLContext

let mlContext = MLContext()

// Load data into IDataView

let dataPath = Path.Join(__SOURCE_DIRECTORY__,"processed-data","*.csv")
let data = mlContext.Data.LoadFromTextFile<ModelInput>(dataPath,separatorChar=',', allowQuoting=true, hasHeader=false)

// Split into training and test sets

let dataSplit = mlContext.Data.TrainTestSplit(data, samplingKeyColumnName="Camis")

// Train model using AutoML
let experimentTimeInSeconds = 300u
let labelColumnName = "InspectionScore"
let experimentResult = mlContext.Auto().CreateRegressionExperiment(experimentTimeInSeconds).Execute(dataSplit.TrainSet, labelColumnName)

// Evaluate the model

let bestModel = experimentResult.BestRun.Model

let predictions = bestModel.Transform dataSplit.TestSet
let metrics = mlContext.Regression.Evaluate(predictions,"InspectionScore","Score")

metrics

// Make predictions with the model

let input = {
    Camis="41720083"
    Boro="Manhattan"
    InspectionType="Cycle Inspection / Re-inspection"
    InspectionScore=11.0f
    Violations= "04H,09C,10F"
    CriticalViolations=1.0f
    TotalViolations=3.0f
}

let predictionEngine= mlContext.Model.CreatePredictionEngine<ModelInput,ModelOutput> bestModel

let prediction = predictionEngine.Predict(input)

prediction

// Save the model
mlContext.Model.Save(bestModel, data.Schema,"InspectionModel.zip")