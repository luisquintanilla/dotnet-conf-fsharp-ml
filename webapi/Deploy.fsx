#r "nuget:Farmer"

open Farmer
open Farmer.Builders

// Update name with the name of your application
let myWebApp = webApp {
    name "FSharpMLWebAPI"
    zip_deploy "webapi/deploy"
}

let deployment = arm {
    location Location.CentralUS
    add_resource myWebApp
}

// Update 'netconf-fsharp' with your own resource group
deployment
|> Deploy.execute "netconf-fsharp" Deploy.NoParameters