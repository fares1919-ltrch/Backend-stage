```plantuml
@startuml Pipeline Flow

' Pipeline Stages
node "Build Stage" {
    [Restore Packages] --> [Build Solution]
    [Build Solution] --> [Run Tests]
    [Run Tests] --> [Publish Artifacts]
}

node "Development Deployment" {
    [Deploy to Dev Environment]
}

node "Production Deployment" {
    [Deploy to Prod Environment]
}

' Connections
[Publish Artifacts] --> [Deploy to Dev Environment] : develop branch
[Publish Artifacts] --> [Deploy to Prod Environment] : master branch

' Branch Policies
note right of [Deploy to Dev Environment]
  Requires:
  - Develop branch
  - Environment approval
end note

note right of [Deploy to Prod Environment]
  Requires:
  - Master branch
  - Environment approval
end note

@enduml
```

## Pipeline Flow Documentation

### Build Stage

1. **Restore Packages**: Retrieves all NuGet dependencies
2. **Build Solution**: Compiles the .NET 8.0 application
3. **Run Tests**: Executes test projects
4. **Publish Artifacts**: Creates deployment package

### Deployment Stages

1. **Development**

   - Triggers on develop branch
   - Requires environment approval
   - Deploys to dev environment

2. **Production**
   - Triggers on master branch
   - Requires environment approval
   - Deploys to production environment

### Required Resources

- Azure Subscription
- Azure Web Apps (dev & prod)
- Azure DevOps Service Connection
- Environment Approvers
