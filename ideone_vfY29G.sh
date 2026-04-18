<Activity mc:Ignorable="sap sap2010" x:Class="Application_Screen_Navigate" xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities" xmlns:i="clr-namespace:IAP_Logging;assembly=IAP.Logging" xmlns:mc="http://s...content-available-to-author-only...s.org/markup-compatibility/2006" xmlns:s="clr-namespace:System;assembly=System.Private.CoreLib" xmlns:sap="http://schemas.microsoft.com/netfx/2009/xaml/activities/presentation" xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation" xmlns:scg="clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib" xmlns:sco="clr-namespace:System.Collections.ObjectModel;assembly=System.Private.CoreLib" xmlns:ui="http://s...content-available-to-author-only...h.com/workflow/activities" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <x:Members>
    <x:Property Name="in_intTimeoutM" Type="InArgument(x:Int32)" />
    <x:Property Name="in_intTimeoutL" Type="InArgument(x:Int32)" />
    <x:Property Name="out_boolActionSuccess" Type="OutArgument(x:Boolean)" />
    <x:Property Name="in_strSourceSelector" Type="InArgument(x:String)" />
    <x:Property Name="in_strTargetSelector" Type="InArgument(x:String)" />
    <x:Property Name="in_intNumberOfRetries" Type="InArgument(x:Int32)" />
    <x:Property Name="in_tspRetryInterval" Type="InArgument(x:TimeSpan)" />
  </x:Members>
  <VisualBasic.Settings>
    <x:Null />
  </VisualBasic.Settings>
  <sap:VirtualizedContainerService.HintSize>1352,988</sap:VirtualizedContainerService.HintSize>
  <sap2010:WorkflowViewState.IdRef>Main_1</sap2010:WorkflowViewState.IdRef>
  <TextExpression.NamespacesForImplementation>
    <scg:List x:TypeArguments="x:String" Capacity="60">
      <x:String>IAP_Logging</x:String>
      <x:String>Microsoft.VisualBasic</x:String>
      <x:String>Microsoft.VisualBasic.Activities</x:String>
      <x:String>Newtonsoft.Json</x:String>
      <x:String>Newtonsoft.Json.Linq</x:String>
      <x:String>System</x:String>
      <x:String>System.Activities</x:String>
      <x:String>System.Activities.Expressions</x:String>
      <x:String>System.Activities.Statements</x:String>
      <x:String>System.Activities.Validation</x:String>
      <x:String>System.Activities.XamlIntegration</x:String>
      <x:String>System.Collections</x:String>
      <x:String>System.Collections.Generic</x:String>
      <x:String>System.Collections.ObjectModel</x:String>
      <x:String>System.Data</x:String>
      <x:String>System.Diagnostics</x:String>
      <x:String>System.Drawing</x:String>
      <x:String>System.Globalization</x:String>
      <x:String>System.IO</x:String>
      <x:String>System.Linq</x:String>
      <x:String>System.Net.Mail</x:String>
      <x:String>System.Reflection</x:String>
      <x:String>System.Runtime.InteropServices</x:String>
      <x:String>System.Runtime.Serialization</x:String>
      <x:String>System.Windows.Markup</x:String>
      <x:String>System.Xml</x:String>
      <x:String>System.Xml.Linq</x:String>
      <x:String>UiPath.AmazonWebServices.Activities</x:String>
      <x:String>UiPath.AmazonWebServices.Activities.S3.Objects</x:String>
      <x:String>UiPath.AmazonWebServices.Models</x:String>
      <x:String>UiPath.Core</x:String>
      <x:String>UiPath.Core.Activities</x:String>
      <x:String>GlobalVariablesNamespace</x:String>
      <x:String>GlobalConstantsNamespace</x:String>
    </scg:List>
  </TextExpression.NamespacesForImplementation>
  <TextExpression.ReferencesForImplementation>
    <sco:Collection x:TypeArguments="AssemblyReference">
      <AssemblyReference>IAP.Logging</AssemblyReference>
      <AssemblyReference>Microsoft.Bcl.AsyncInterfaces</AssemblyReference>
      <AssemblyReference>Microsoft.VisualBasic</AssemblyReference>
      <AssemblyReference>mscorlib</AssemblyReference>
      <AssemblyReference>Newtonsoft.Json</AssemblyReference>
      <AssemblyReference>PresentationCore</AssemblyReference>
      <AssemblyReference>PresentationFramework</AssemblyReference>
      <AssemblyReference>System</AssemblyReference>
      <AssemblyReference>System.Activities</AssemblyReference>
      <AssemblyReference>System.Collections</AssemblyReference>
      <AssemblyReference>System.ComponentModel.Composition</AssemblyReference>
      <AssemblyReference>System.ComponentModel.TypeConverter</AssemblyReference>
      <AssemblyReference>System.Core</AssemblyReference>
      <AssemblyReference>System.Data</AssemblyReference>
      <AssemblyReference>System.Drawing</AssemblyReference>
      <AssemblyReference>System.Linq</AssemblyReference>
      <AssemblyReference>System.Memory</AssemblyReference>
      <AssemblyReference>System.ObjectModel</AssemblyReference>
      <AssemblyReference>System.Private.CoreLib</AssemblyReference>
      <AssemblyReference>System.Runtime.Serialization</AssemblyReference>
      <AssemblyReference>System.ServiceModel</AssemblyReference>
      <AssemblyReference>System.ValueTuple</AssemblyReference>
      <AssemblyReference>System.Xaml</AssemblyReference>
      <AssemblyReference>System.Xml</AssemblyReference>
      <AssemblyReference>System.Xml.Linq</AssemblyReference>
      <AssemblyReference>UiPath.AmazonWebServices</AssemblyReference>
      <AssemblyReference>UiPath.AmazonWebServices.Activities</AssemblyReference>
      <AssemblyReference>UiPath.Excel</AssemblyReference>
      <AssemblyReference>UiPath.Mail</AssemblyReference>
      <AssemblyReference>UiPath.OCR.Activities.Design</AssemblyReference>
      <AssemblyReference>UiPath.Studio.Constants</AssemblyReference>
      <AssemblyReference>UiPath.System.Activities</AssemblyReference>
      <AssemblyReference>UiPath.System.Activities.Design</AssemblyReference>
      <AssemblyReference>UiPath.UiAutomation.Activities</AssemblyReference>
      <AssemblyReference>UiPath.UIAutomationCore</AssemblyReference>
      <AssemblyReference>UiPath.Workflow</AssemblyReference>
      <AssemblyReference>WindowsBase</AssemblyReference>
    </sco:Collection>
  </TextExpression.ReferencesForImplementation>
  <sap:WorkflowViewStateService.ViewState>
    <scg:Dictionary x:TypeArguments="x:String, x:Object">
      <x:Boolean x:Key="ShouldExpandAll">False</x:Boolean>
    </scg:Dictionary>
  </sap:WorkflowViewStateService.ViewState>
  <Sequence sap2010:Annotation.AnnotationText="Component Name: &lt;Application&gt;_&lt;Screen&gt;_&lt;Function&gt;&#xA;Description: Narrative of what tasks the component will perform&#xA;Pre Condition: #NA&#xA;Post Condition: #NA &#xA;PDD Section: #NA" DisplayName="Application_Screen_Navigate" sap:VirtualizedContainerService.HintSize="1193,2603" sap2010:WorkflowViewState.IdRef="Sequence_1">
    <Sequence.Variables>
      <Variable x:TypeArguments="x:String" Default="Application_Screen_Navigate" Name="strComponentName" />
    </Sequence.Variables>
    <sap:WorkflowViewStateService.ViewState>
      <scg:Dictionary x:TypeArguments="x:String, x:Object">
        <x:Boolean x:Key="IsExpanded">True</x:Boolean>
        <x:Boolean x:Key="IsAnnotationDocked">True</x:Boolean>
      </scg:Dictionary>
    </sap:WorkflowViewStateService.ViewState>
    <i:Process_Monitoring___Component___Start DisplayName="Process Monitoring - Component - Start" sap:VirtualizedContainerService.HintSize="632,122" sap2010:WorkflowViewState.IdRef="Process_Monitoring___Component___Start_1" StrFileName="[strComponentName]" />
    <TryCatch DisplayName="Try catch" sap:VirtualizedContainerService.HintSize="632,2386" sap2010:WorkflowViewState.IdRef="TryCatch_1">
      <TryCatch.Try>
        <Sequence DisplayName="Try Sequence" sap:VirtualizedContainerService.HintSize="594,2135" sap2010:WorkflowViewState.IdRef="Sequence_12">
          <sap:WorkflowViewStateService.ViewState>
            <scg:Dictionary x:TypeArguments="x:String, x:Object">
              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
              <x:Boolean x:Key="IsPinned">False</x:Boolean>
            </scg:Dictionary>
          </sap:WorkflowViewStateService.ViewState>
          <ui:RetryScope DisplayName="Retry Scope" sap:VirtualizedContainerService.HintSize="560,1860" sap2010:WorkflowViewState.IdRef="RetryScope_1" NumberOfRetries="[in_intNumberOfRetries]" RetryInterval="[in_tspRetryInterval]">
            <ui:RetryScope.ActivityBody>
              <ActivityAction>
                <Sequence DisplayName="Work Sequence" sap:VirtualizedContainerService.HintSize="518,1653" sap2010:WorkflowViewState.IdRef="Sequence_19">
                  <sap:WorkflowViewStateService.ViewState>
                    <scg:Dictionary x:TypeArguments="x:String, x:Object">
                      <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                      <x:Boolean x:Key="IsPinned">False</x:Boolean>
                    </scg:Dictionary>
                  </sap:WorkflowViewStateService.ViewState>
                  <Sequence DisplayName="Pre Condition" sap:VirtualizedContainerService.HintSize="484,1374" sap2010:WorkflowViewState.IdRef="Sequence_15">
                    <Sequence.Variables>
                      <Variable x:TypeArguments="x:Boolean" Name="boolPreConditionMet" />
                      <Variable x:TypeArguments="x:String" Name="variable1" />
                    </Sequence.Variables>
                    <sap:WorkflowViewStateService.ViewState>
                      <scg:Dictionary x:TypeArguments="x:String, x:Object">
                        <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                        <x:Boolean x:Key="IsPinned">False</x:Boolean>
                      </scg:Dictionary>
                    </sap:WorkflowViewStateService.ViewState>
                    <ui:UiElementExists DisplayName="Pre-Condition" Exists="[boolPreConditionMet]" sap:VirtualizedContainerService.HintSize="450,119" sap2010:WorkflowViewState.IdRef="UiElementExists_5">
                      <ui:UiElementExists.Target>
                        <ui:Target ClippingRegion="{x:Null}" Element="{x:Null}" Id="1f4d7170-4ae4-4430-89c2-5fb5a45aa257" Selector="[in_strSourceSelector]" TimeoutMS="[in_intTimeoutL]" WaitForReady="COMPLETE" />
                      </ui:UiElementExists.Target>
                    </ui:UiElementExists>
                    <If Condition="[boolPreConditionMet]" DisplayName="Precondition met?" sap:VirtualizedContainerService.HintSize="450,432" sap2010:WorkflowViewState.IdRef="If_6">
                      <If.Then>
                        <Sequence DisplayName="Then" sap:VirtualizedContainerService.HintSize="416,89" sap2010:WorkflowViewState.IdRef="Sequence_23">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                        </Sequence>
                      </If.Then>
                      <If.Else>
                        <Sequence DisplayName="Precondition failed" sap:VirtualizedContainerService.HintSize="416,173" sap2010:WorkflowViewState.IdRef="Sequence_24">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                          <Throw DisplayName="Throw - SE" Exception="[New Exception (&quot;Pre Condition Failed: &lt;target screen/element&gt; could not be found&quot;)]" sap:VirtualizedContainerService.HintSize="382,113" sap2010:WorkflowViewState.IdRef="Throw_7" />
                        </Sequence>
                      </If.Else>
                    </If>
                    <If Condition="[boolPreConditionMet]" DisplayName="Precondition met?" sap:VirtualizedContainerService.HintSize="450,731" sap2010:WorkflowViewState.IdRef="If_4">
                      <If.Then>
                        <Sequence DisplayName="Then" sap:VirtualizedContainerService.HintSize="416,224" sap2010:WorkflowViewState.IdRef="Sequence_21">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                          <i:Info_Log StrMessage="{x:Null}" DisplayName="Info Log" sap:VirtualizedContainerService.HintSize="382,164" sap2010:WorkflowViewState.IdRef="Info_Log_3" StrTag="info" />
                        </Sequence>
                      </If.Then>
                      <If.Else>
                        <Sequence DisplayName="Precondition failed" sap:VirtualizedContainerService.HintSize="416,346" sap2010:WorkflowViewState.IdRef="Sequence_14">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                          <i:Error_Log StrMessage="{x:Null}" DisplayName="Error Log" sap:VirtualizedContainerService.HintSize="382,173" sap2010:WorkflowViewState.IdRef="Error_Log_3" StrTag="error" />
                          <Throw DisplayName="Throw - SE" Exception="[New Exception (&quot;Pre Condition Failed: &lt;target screen/element&gt; could not be found&quot;)]" sap:VirtualizedContainerService.HintSize="382,113" sap2010:WorkflowViewState.IdRef="Throw_5" />
                        </Sequence>
                      </If.Else>
                    </If>
                  </Sequence>
                  <Sequence DisplayName="Do Work" sap:VirtualizedContainerService.HintSize="484,57" sap2010:WorkflowViewState.IdRef="Sequence_16">
                    <sap:WorkflowViewStateService.ViewState>
                      <scg:Dictionary x:TypeArguments="x:String, x:Object">
                        <x:Boolean x:Key="IsExpanded">False</x:Boolean>
                        <x:Boolean x:Key="IsPinned">False</x:Boolean>
                      </scg:Dictionary>
                    </sap:WorkflowViewStateService.ViewState>
                    <ui:Comment sap:VirtualizedContainerService.HintSize="338,75" sap2010:WorkflowViewState.IdRef="Comment_2" Text="// Add appropriate activity to cover required steps" />
                  </Sequence>
                  <Sequence DisplayName="Post Condition" sap:VirtualizedContainerService.HintSize="484,57" sap2010:WorkflowViewState.IdRef="Sequence_18">
                    <Sequence.Variables>
                      <Variable x:TypeArguments="x:Boolean" Name="boolPostConditionMet" />
                    </Sequence.Variables>
                    <sap:WorkflowViewStateService.ViewState>
                      <scg:Dictionary x:TypeArguments="x:String, x:Object">
                        <x:Boolean x:Key="IsExpanded">False</x:Boolean>
                        <x:Boolean x:Key="IsPinned">False</x:Boolean>
                      </scg:Dictionary>
                    </sap:WorkflowViewStateService.ViewState>
                    <ui:UiElementExists DisplayName="Post-Condition" Exists="[boolPostConditionMet]" sap:VirtualizedContainerService.HintSize="645,69" sap2010:WorkflowViewState.IdRef="UiElementExists_6">
                      <ui:UiElementExists.Target>
                        <ui:Target ClippingRegion="{x:Null}" Element="{x:Null}" Id="c931b483-7f04-434f-befc-54414ef97830" Selector="[in_strTargetSelector]" TimeoutMS="[in_intTimeoutM]" WaitForReady="COMPLETE" />
                      </ui:UiElementExists.Target>
                    </ui:UiElementExists>
                    <If Condition="[boolPostConditionMet]" DisplayName="Postcondition met?" sap:VirtualizedContainerService.HintSize="645,360" sap2010:WorkflowViewState.IdRef="If_5">
                      <If.Then>
                        <Sequence sap2010:WorkflowViewState.IdRef="Sequence_22">
                          <i:Info_Log StrMessage="{x:Null}" DisplayName="Info Log" sap:VirtualizedContainerService.HintSize="200,25" sap2010:WorkflowViewState.IdRef="Info_Log_4" StrTag="info" />
                        </Sequence>
                      </If.Then>
                      <If.Else>
                        <Sequence DisplayName="Target not found" sap:VirtualizedContainerService.HintSize="400,201" sap2010:WorkflowViewState.IdRef="Sequence_17">
                          <sap:WorkflowViewStateService.ViewState>
                            <scg:Dictionary x:TypeArguments="x:String, x:Object">
                              <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                            </scg:Dictionary>
                          </sap:WorkflowViewStateService.ViewState>
                          <i:Error_Log StrMessage="{x:Null}" DisplayName="Error Log" sap:VirtualizedContainerService.HintSize="338,25" sap2010:WorkflowViewState.IdRef="Error_Log_4" StrTag="error" />
                          <Throw DisplayName="Throw - SE" Exception="[New Exception (&quot;Post Condition Failed - &lt;target screen/element&gt; could not be found&quot;)]" sap:VirtualizedContainerService.HintSize="338,25" sap2010:WorkflowViewState.IdRef="Throw_6" />
                        </Sequence>
                      </If.Else>
                    </If>
                  </Sequence>
                  <Assign DisplayName="Status success" sap:VirtualizedContainerService.HintSize="484,105" sap2010:WorkflowViewState.IdRef="Assign_2">
                    <Assign.To>
                      <OutArgument x:TypeArguments="x:Boolean">[out_boolActionSuccess]</OutArgument>
                    </Assign.To>
                    <Assign.Value>
                      <InArgument x:TypeArguments="x:Boolean">[True]</InArgument>
                    </Assign.Value>
                  </Assign>
                </Sequence>
              </ActivityAction>
            </ui:RetryScope.ActivityBody>
            <ui:RetryScope.Condition>
              <ActivityFunc x:TypeArguments="x:Boolean" />
            </ui:RetryScope.Condition>
          </ui:RetryScope>
          <i:Process_Monitoring___Component___End StrExecutionMessage="{x:Null}" BoolSuccess="True" DisplayName="Process Monitoring - Component - End" sap:VirtualizedContainerService.HintSize="560,215" sap2010:WorkflowViewState.IdRef="Process_Monitoring___Component___End_1" StrFileName="[strComponentName]" />
        </Sequence>
      </TryCatch.Try>
      <TryCatch.Catches>
        <Catch x:TypeArguments="s:Exception" sap:VirtualizedContainerService.HintSize="598,22" sap2010:WorkflowViewState.IdRef="Catch`1_1">
          <sap:WorkflowViewStateService.ViewState>
            <scg:Dictionary x:TypeArguments="x:String, x:Object">
              <x:Boolean x:Key="IsExpanded">False</x:Boolean>
              <x:Boolean x:Key="IsPinned">False</x:Boolean>
            </scg:Dictionary>
          </sap:WorkflowViewStateService.ViewState>
          <ActivityAction x:TypeArguments="s:Exception">
            <ActivityAction.Argument>
              <DelegateInArgument x:TypeArguments="s:Exception" Name="exception" />
            </ActivityAction.Argument>
            <Sequence DisplayName="Exception Sequence" sap:VirtualizedContainerService.HintSize="416,135" sap2010:WorkflowViewState.IdRef="Sequence_20">
              <sap:WorkflowViewStateService.ViewState>
                <scg:Dictionary x:TypeArguments="x:String, x:Object">
                  <x:Boolean x:Key="IsExpanded">True</x:Boolean>
                  <x:Boolean x:Key="IsPinned">False</x:Boolean>
                </scg:Dictionary>
              </sap:WorkflowViewStateService.ViewState>
              <i:Error_Log DisplayName="Error Log - SE (Failed)" sap:VirtualizedContainerService.HintSize="338,25" sap2010:WorkflowViewState.IdRef="Error_Log_6" StrMessage="[strComponentName + &quot;, Failed to execute successfully due to the following exception:&quot;+ &quot; &quot; +&#xA;&quot;ExMessage: &quot; + exception.Message + &quot; &quot; +&#xA;&quot;ExSource: &quot; + exception.Source + &quot; &quot;]" StrTag="error" />
              <i:Process_Monitoring___Component___End BoolSuccess="False" DisplayName="Process Monitoring - Component - End" sap:VirtualizedContainerService.HintSize="338,25" sap2010:WorkflowViewState.IdRef="Process_Monitoring___Component___End_3" StrExecutionMessage="[exception.Message]" StrFileName="[strComponentName]" />
              <Rethrow DisplayName="Rethrow - SE" sap:VirtualizedContainerService.HintSize="338,25" sap2010:WorkflowViewState.IdRef="Rethrow_2" />
            </Sequence>
          </ActivityAction>
        </Catch>
      </TryCatch.Catches>
    </TryCatch>
  </Sequence>
</Activity>