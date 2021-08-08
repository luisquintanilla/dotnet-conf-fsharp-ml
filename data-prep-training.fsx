(*TRAIN THE MODEL*)
// In this section, we'll take the data that was processed using .NET for Apache Spark and use it for training

// Install and reference ML.NET & AutoML packages

printfn "Start Model Training. Please be patient."

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

printfn "Finished training the model."