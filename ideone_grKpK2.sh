<Activity mc:Ignorable="sap sap2010" x:Class="Test_Bench" VisualBasic.Settings="{x:Null}" sap2010:WorkflowViewState.IdRef="ActivityBuilder_1" xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" xmlns:av="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:i="clr-namespace:IAP_Logging;assembly=IAP.Logging" xmlns:mc="http://s...content-available-to-author-only...s.org/markup-compatibility/2006" xmlns:sap="http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation" xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation" xmlns:scg="clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib" xmlns:sco="clr-namespace:System.Collections.ObjectModel;assembly=System.Private.CoreLib" xmlns:ui="http://s...content-available-to-author-only...h.com/workflow/activities" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextExpression.NamespacesForImplementation>
    <sco:Collection x:TypeArguments="x:String">
      <x:String>System.Activities</x:String>
      <x:String>System.Activities.Statements</x:String>
      <x:String>System.Activities.Expressions</x:String>
      <x:String>System.Activities.Validation</x:String>
      <x:String>System.Activities.XamlIntegration</x:String>
      <x:String>Microsoft.VisualBasic</x:String>
      <x:String>Microsoft.VisualBasic.Activities</x:String>
      <x:String>System</x:String>
      <x:String>System.Collections</x:String>
      <x:String>System.Collections.Generic</x:String>
      <x:String>System.Collections.ObjectModel</x:String>
      <x:String>System.Data</x:String>
      <x:String>System.Diagnostics</x:String>
      <x:String>System.Drawing</x:String>
      <x:String>System.IO</x:String>
      <x:String>System.Linq</x:String>
      <x:String>System.Net.Mail</x:String>
      <x:String>System.Xml</x:String>
      <x:String>System.Xml.Linq</x:String>
      <x:String>UiPath.Core</x:String>
      <x:String>UiPath.Core.Activities</x:String>
      <x:String>System.Windows.Markup</x:String>
      <x:String>GlobalVariablesNamespace</x:String>
      <x:String>GlobalConstantsNamespace</x:String>
      <x:String>System.Linq.Expressions</x:String>
      <x:String>System.Runtime.Serialization</x:String>
      <x:String>System.Reflection</x:String>
      <x:String>System.Numerics</x:String>
      <x:String>IAP_Logging</x:String>
    </sco:Collection>
  </TextExpression.NamespacesForImplementation>
  <TextExpression.ReferencesForImplementation>
    <sco:Collection x:TypeArguments="AssemblyReference">
      <AssemblyReference>Microsoft.VisualBasic</AssemblyReference>
      <AssemblyReference>mscorlib</AssemblyReference>
      <AssemblyReference>PresentationCore</AssemblyReference>
      <AssemblyReference>PresentationFramework</AssemblyReference>
      <AssemblyReference>System</AssemblyReference>
      <AssemblyReference>System.Activities</AssemblyReference>
      <AssemblyReference>System.ComponentModel.TypeConverter</AssemblyReference>
      <AssemblyReference>System.Core</AssemblyReference>
      <AssemblyReference>System.Data</AssemblyReference>
      <AssemblyReference>System.Data.Common</AssemblyReference>
      <AssemblyReference>System.Data.DataSetExtensions</AssemblyReference>
      <AssemblyReference>System.Drawing</AssemblyReference>
      <AssemblyReference>System.Drawing.Common</AssemblyReference>
      <AssemblyReference>System.Drawing.Primitives</AssemblyReference>
      <AssemblyReference>System.Linq</AssemblyReference>
      <AssemblyReference>System.Net.Mail</AssemblyReference>
      <AssemblyReference>System.ObjectModel</AssemblyReference>
      <AssemblyReference>System.Private.CoreLib</AssemblyReference>
      <AssemblyReference>System.Xaml</AssemblyReference>
      <AssemblyReference>System.Xml</AssemblyReference>
      <AssemblyReference>System.Xml.Linq</AssemblyReference>
      <AssemblyReference>UiPath.System.Activities</AssemblyReference>
      <AssemblyReference>UiPath.UiAutomation.Activities</AssemblyReference>
      <AssemblyReference>WindowsBase</AssemblyReference>
      <AssemblyReference>UiPath.Studio.Constants</AssemblyReference>
      <AssemblyReference>UiPath.Excel.Activities.Design</AssemblyReference>
      <AssemblyReference>System.Memory.Data</AssemblyReference>
      <AssemblyReference>UiPath.Cryptography</AssemblyReference>
      <AssemblyReference>System.Console</AssemblyReference>
      <AssemblyReference>System.Security.Permissions</AssemblyReference>
      <AssemblyReference>System.Configuration.ConfigurationManager</AssemblyReference>
      <AssemblyReference>System.ComponentModel</AssemblyReference>
      <AssemblyReference>System.Memory</AssemblyReference>
      <AssemblyReference>System.Private.Uri</AssemblyReference>
      <AssemblyReference>System.Linq.Expressions</AssemblyReference>
      <AssemblyReference>System.Private.ServiceModel</AssemblyReference>
      <AssemblyReference>System.Private.DataContractSerialization</AssemblyReference>
      <AssemblyReference>System.Runtime.Serialization.Formatters</AssemblyReference>
      <AssemblyReference>System.Runtime.Serialization.Primitives</AssemblyReference>
      <AssemblyReference>System.Reflection.DispatchProxy</AssemblyReference>
      <AssemblyReference>System.Reflection.TypeExtensions</AssemblyReference>
      <AssemblyReference>System.Reflection.Metadata</AssemblyReference>
      <AssemblyReference>System.Runtime.Numerics</AssemblyReference>
      <AssemblyReference>IAP.Logging</AssemblyReference>
    </sco:Collection>
  </TextExpression.ReferencesForImplementation>
  <Sequence sap:VirtualizedContainerService.HintSize="1193,1442" sap2010:WorkflowViewState.IdRef="Sequence_2">
    <Sequence.Variables>
      <Variable x:TypeArguments="x:Int32" Name="x">
        <Variable.Default>
          <Literal x:TypeArguments="x:Int32" Value="1" />
        </Variable.Default>
      </Variable>
    </Sequence.Variables>
    <sap:WorkflowViewStateService.ViewState>
      <scg:Dictionary x:TypeArguments="x:String, x:Object">
        <x:Boolean x:Key="IsExpanded">True</x:Boolean>
      </scg:Dictionary>
    </sap:WorkflowViewStateService.ViewState>
    <Flowchart sap:VirtualizedContainerService.HintSize="600,57" sap2010:WorkflowViewState.IdRef="Flowchart_1">
      <sap:WorkflowViewStateService.ViewState>
        <scg:Dictionary x:TypeArguments="x:String, x:Object">
          <x:Boolean x:Key="IsExpanded">False</x:Boolean>
          <x:Boolean x:Key="IsPinned">False</x:Boolean>
          <av:Point x:Key="ShapeLocation">275,35</av:Point>
          <av:Size x:Key="ShapeSize">50,50</av:Size>
          <av:PointCollection x:Key="ConnectorLocation">300,85 300,130</av:PointCollection>
        </scg:Dictionary>
      </sap:WorkflowViewStateService.ViewState>
      <Flowchart.StartNode>
        <x:Reference>__ReferenceID0</x:Reference>
      </Flowchart.StartNode>
      <FlowDecision x:Name="__ReferenceID0" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_1">
        <FlowDecision.Condition>
          <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
        </FlowDecision.Condition>
        <sap:WorkflowViewStateService.ViewState>
          <scg:Dictionary x:TypeArguments="x:String, x:Object">
            <x:Boolean x:Key="IsExpanded">True</x:Boolean>
            <av:Point x:Key="ShapeLocation">270,130</av:Point>
            <av:Size x:Key="ShapeSize">60,60</av:Size>
          </scg:Dictionary>
        </sap:WorkflowViewStateService.ViewState>
      </FlowDecision>
    </Flowchart>
    <Flowchart sap:VirtualizedContainerService.HintSize="600,657" sap2010:WorkflowViewState.IdRef="Flowchart_2">
      <sap:WorkflowViewStateService.ViewState>
        <scg:Dictionary x:TypeArguments="x:String, x:Object">
          <x:Boolean x:Key="IsExpanded">True</x:Boolean>
          <x:Boolean x:Key="IsPinned">False</x:Boolean>
          <av:Point x:Key="ShapeLocation">275,35</av:Point>
          <av:Size x:Key="ShapeSize">50,50</av:Size>
          <av:PointCollection x:Key="ConnectorLocation">300,85 300,130</av:PointCollection>
        </scg:Dictionary>
      </sap:WorkflowViewStateService.ViewState>
      <Flowchart.StartNode>
        <x:Reference>__ReferenceID4</x:Reference>
      </Flowchart.StartNode>
      <FlowDecision x:Name="__ReferenceID4" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_2">
        <FlowDecision.Condition>
          <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
        </FlowDecision.Condition>
        <sap:WorkflowViewStateService.ViewState>
          <scg:Dictionary x:TypeArguments="x:String, x:Object">
            <x:Boolean x:Key="IsExpanded">True</x:Boolean>
            <av:Point x:Key="ShapeLocation">270,130</av:Point>
            <av:Size x:Key="ShapeSize">60,60</av:Size>
            <av:PointCollection x:Key="TrueConnector">270,160 215,160</av:PointCollection>
          </scg:Dictionary>
        </sap:WorkflowViewStateService.ViewState>
        <FlowDecision.True>
          <FlowStep x:Name="__ReferenceID1">
            <sap:WorkflowViewStateService.ViewState>
              <scg:Dictionary x:TypeArguments="x:String, x:Object">
                <av:Point x:Key="ShapeLocation">105,125</av:Point>
                <av:Size x:Key="ShapeSize">110,70</av:Size>
                <av:PointCollection x:Key="ConnectorLocation">160,195 160,240</av:PointCollection>
              </scg:Dictionary>
            </sap:WorkflowViewStateService.ViewState>
            <i:Info_Log StrMessage="{x:Null}" DisplayName="Info Log" sap:VirtualizedContainerService.HintSize="110,70" sap2010:WorkflowViewState.IdRef="Info_Log_1" StrTag="info">
              <sap:WorkflowViewStateService.ViewState>
                <scg:Dictionary x:TypeArguments="x:String, x:Object">
                  <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                </scg:Dictionary>
              </sap:WorkflowViewStateService.ViewState>
            </i:Info_Log>
            <FlowStep.Next>
              <FlowDecision x:Name="__ReferenceID2" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_3">
                <FlowDecision.Condition>
                  <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
                </FlowDecision.Condition>
                <sap:WorkflowViewStateService.ViewState>
                  <scg:Dictionary x:TypeArguments="x:String, x:Object">
                    <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                    <av:Point x:Key="ShapeLocation">130,240</av:Point>
                    <av:Size x:Key="ShapeSize">60,60</av:Size>
                    <av:PointCollection x:Key="TrueConnector">190,270 235,270</av:PointCollection>
                  </scg:Dictionary>
                </sap:WorkflowViewStateService.ViewState>
                <FlowDecision.True>
                  <FlowStep x:Name="__ReferenceID3">
                    <sap:WorkflowViewStateService.ViewState>
                      <scg:Dictionary x:TypeArguments="x:String, x:Object">
                        <av:Point x:Key="ShapeLocation">235,235</av:Point>
                        <av:Size x:Key="ShapeSize">110,70</av:Size>
                      </scg:Dictionary>
                    </sap:WorkflowViewStateService.ViewState>
                    <If Condition="[x&gt;5]" sap:VirtualizedContainerService.HintSize="518,761" sap2010:WorkflowViewState.IdRef="If_1">
                      <sap:WorkflowViewStateService.ViewState>
                        <scg:Dictionary x:TypeArguments="x:String, x:Object">
                          <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                        </scg:Dictionary>
                      </sap:WorkflowViewStateService.ViewState>
                      <If.Then>
                        <Sequence DisplayName="Then" sap:VirtualizedContainerService.HintSize="484,534" sap2010:WorkflowViewState.IdRef="Sequence_3">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                          <If Condition="[x&gt;5]" sap:VirtualizedContainerService.HintSize="450,474" sap2010:WorkflowViewState.IdRef="If_2">
                            <If.Then>
                              <Sequence DisplayName="Then" sap:VirtualizedContainerService.HintSize="416,224" sap2010:WorkflowViewState.IdRef="Sequence_5">
                                <sap:WorkflowViewStateService.ViewState>
                                  <scg:Dictionary x:TypeArguments="x:String, x:Object">
                                    <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                                  </scg:Dictionary>
                                </sap:WorkflowViewStateService.ViewState>
                                <i:Info_Log StrMessage="{x:Null}" DisplayName="Info Log" sap:VirtualizedContainerService.HintSize="382,164" sap2010:WorkflowViewState.IdRef="Info_Log_2" StrTag="info" />
                              </Sequence>
                            </If.Then>
                            <If.Else>
                              <Sequence DisplayName="Else" sap:VirtualizedContainerService.HintSize="416,89" sap2010:WorkflowViewState.IdRef="Sequence_6">
                                <sap:WorkflowViewStateService.ViewState>
                                  <scg:Dictionary x:TypeArguments="x:String, x:Object">
                                    <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                                  </scg:Dictionary>
                                </sap:WorkflowViewStateService.ViewState>
                              </Sequence>
                            </If.Else>
                          </If>
                        </Sequence>
                      </If.Then>
                      <If.Else>
                        <Sequence DisplayName="Else" sap:VirtualizedContainerService.HintSize="416,89" sap2010:WorkflowViewState.IdRef="Sequence_4">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                        </Sequence>
                      </If.Else>
                    </If>
                  </FlowStep>
                </FlowDecision.True>
              </FlowDecision>
            </FlowStep.Next>
          </FlowStep>
        </FlowDecision.True>
      </FlowDecision>
      <x:Reference>__ReferenceID1</x:Reference>
      <x:Reference>__ReferenceID2</x:Reference>
      <x:Reference>__ReferenceID3</x:Reference>
    </Flowchart>
    <Flowchart sap:VirtualizedContainerService.HintSize="600,657" sap2010:WorkflowViewState.IdRef="Flowchart_3">
      <sap:WorkflowViewStateService.ViewState>
        <scg:Dictionary x:TypeArguments="x:String, x:Object">
          <x:Boolean x:Key="IsExpanded">True</x:Boolean>
          <x:Boolean x:Key="IsPinned">False</x:Boolean>
          <av:Point x:Key="ShapeLocation">275,35</av:Point>
          <av:Size x:Key="ShapeSize">50,50</av:Size>
          <av:PointCollection x:Key="ConnectorLocation">300,85 300,130</av:PointCollection>
        </scg:Dictionary>
      </sap:WorkflowViewStateService.ViewState>
      <Flowchart.StartNode>
        <x:Reference>__ReferenceID8</x:Reference>
      </Flowchart.StartNode>
      <FlowDecision x:Name="__ReferenceID8" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_4">
        <FlowDecision.Condition>
          <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
        </FlowDecision.Condition>
        <sap:WorkflowViewStateService.ViewState>
          <scg:Dictionary x:TypeArguments="x:String, x:Object">
            <x:Boolean x:Key="IsExpanded">True</x:Boolean>
            <av:Point x:Key="ShapeLocation">270,130</av:Point>
            <av:Size x:Key="ShapeSize">60,60</av:Size>
            <av:PointCollection x:Key="TrueConnector">270,160 215,160</av:PointCollection>
            <av:PointCollection x:Key="FalseConnector">330,160 380,160</av:PointCollection>
          </scg:Dictionary>
        </sap:WorkflowViewStateService.ViewState>
        <FlowDecision.True>
          <FlowStep x:Name="__ReferenceID5">
            <sap:WorkflowViewStateService.ViewState>
              <scg:Dictionary x:TypeArguments="x:String, x:Object">
                <av:Point x:Key="ShapeLocation">105,125</av:Point>
                <av:Size x:Key="ShapeSize">110,70</av:Size>
              </scg:Dictionary>
            </sap:WorkflowViewStateService.ViewState>
            <ui:MultipleAssign DisplayName="Multiple Assign" sap:VirtualizedContainerService.HintSize="110,70" sap2010:WorkflowViewState.IdRef="MultipleAssign_1">
              <ui:MultipleAssign.AssignOperations>
                <scg:List x:TypeArguments="ui:AssignOperation" Capacity="4">
                  <ui:AssignOperation sap2010:WorkflowViewState.IdRef="AssignOperation_1">
                    <ui:AssignOperation.To>
                      <OutArgument x:TypeArguments="x:Int32">[x]</OutArgument>
                    </ui:AssignOperation.To>
                    <ui:AssignOperation.Value>
                      <InArgument x:TypeArguments="x:Int32">3</InArgument>
                    </ui:AssignOperation.Value>
                  </ui:AssignOperation>
                </scg:List>
              </ui:MultipleAssign.AssignOperations>
              <sap:WorkflowViewStateService.ViewState>
                <scg:Dictionary x:TypeArguments="x:String, x:Object">
                  <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                </scg:Dictionary>
              </sap:WorkflowViewStateService.ViewState>
            </ui:MultipleAssign>
          </FlowStep>
        </FlowDecision.True>
        <FlowDecision.False>
          <FlowDecision x:Name="__ReferenceID6" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_5">
            <FlowDecision.Condition>
              <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
            </FlowDecision.Condition>
            <sap:WorkflowViewStateService.ViewState>
              <scg:Dictionary x:TypeArguments="x:String, x:Object">
                <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                <av:Point x:Key="ShapeLocation">380,130</av:Point>
                <av:Size x:Key="ShapeSize">60,60</av:Size>
                <av:PointCollection x:Key="TrueConnector">410,190 410,240</av:PointCollection>
              </scg:Dictionary>
            </sap:WorkflowViewStateService.ViewState>
            <FlowDecision.True>
              <FlowDecision x:Name="__ReferenceID7" DisplayName="Flow Decision" sap:VirtualizedContainerService.HintSize="60,60" sap2010:WorkflowViewState.IdRef="FlowDecision_6">
                <FlowDecision.Condition>
                  <VisualBasicValue x:TypeArguments="x:Boolean" DisplayName="Visual basic value" ExpressionText="x&gt;5" />
                </FlowDecision.Condition>
                <sap:WorkflowViewStateService.ViewState>
                  <scg:Dictionary x:TypeArguments="x:String, x:Object">
                    <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                    <av:Point x:Key="ShapeLocation">380,240</av:Point>
                    <av:Size x:Key="ShapeSize">60,60</av:Size>
                  </scg:Dictionary>
                </sap:WorkflowViewStateService.ViewState>
              </FlowDecision>
            </FlowDecision.True>
          </FlowDecision>
        </FlowDecision.False>
      </FlowDecision>
      <x:Reference>__ReferenceID5</x:Reference>
      <x:Reference>__ReferenceID6</x:Reference>
      <x:Reference>__ReferenceID7</x:Reference>
    </Flowchart>
    <DynamicActivity Name="{x:Null}" DisplayName="End" sap:VirtualizedContainerService.HintSize="600,48" sap2010:WorkflowViewState.IdRef="DynamicActivity_1" />
  </Sequence>
</Activity>