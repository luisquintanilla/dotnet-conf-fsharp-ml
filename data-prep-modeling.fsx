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

printfn "Finished preparing the model. Please stop `spark-debug.sh`."