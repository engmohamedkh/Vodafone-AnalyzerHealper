using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace AnnotationsSpace
{
    public class Annotations : IRegisterAnalyzerConfiguration
    {
        // check list size
        public static bool IsEmpty<T>(List<T> list)
        {
            if (list == null)
            {
                return true;
            }

            return !list.Any();
        }

        // Configs
        string RuleID = "VF-033";
        string RuleName = "Annotations";
        string WFAnalyzerVersion = "WorkflowAnalyzerV4";
        // Excluded WF from the rule
        string ExcludedWorkFlows = "";

        //Rule init
        public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
        {
            if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
                return;

            var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, AnnotationsImplementation)
            {
                /// Off and Verbose are not supported.
                ErrorLevel = System.Diagnostics.TraceLevel.Warning,
                RecommendationMessage = "Checking the annotations for every workflow except framework workflows."
            };

            workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
        }

        // Rule implementation
        private InspectionResult AnnotationsImplementation(IWorkflowModel CurrentWorkflow, Rule theNewRule)
        {
            var messageList = new List<string>();

            try
            {
                
                // read the current workflow as a text of lines
                string[] XamlFileLines = File.ReadAllLines(CurrentWorkflow.Project.Directory.ToString() + "\\" + CurrentWorkflow.RelativePath);



                // ExceptedWorkFlows always success
                if ((ExcludedWorkFlows.Contains(CurrentWorkflow.DisplayName)) || (CurrentWorkflow.DisplayName.Contains("IAP_")) || (CurrentWorkflow.DisplayName.Contains("ProcessName_")) || (CurrentWorkflow.DisplayName.Contains("Subprocess")))
                {
                    // No Error message existed return Success
                    return new InspectionResult()
                    {
                        HasErrors = false
                    };
                }
                /*messageList.Add(CurrentWorkflow.Root.ToString());
                messageList.Add(XamlFileLines.ToList().FirstOrDefault(x => x.Contains("<Sequence")));
                messageList.Add(CurrentWorkflow.Root.ToolboxName);*/
                // no sequence or workflow added
                if (CurrentWorkflow.Root == null)
                {
                    // No Error message existed return Success
                    messageList.Add(string.Format("The following workflow: {0} is empty.", CurrentWorkflow.DisplayName));
                }
                // For any WF type (Sequence or Flowchart)
                else if (CurrentWorkflow.Root.ToolboxName.Contains("Sequence") || CurrentWorkflow.Root.ToolboxName.Contains("Flowchart"))
                {
                    // Getting the annotations line to do the logic
                    string annotationLine = null;

                    if (CurrentWorkflow.Root.ToolboxName.Contains("Sequence"))
                    {
                        // get the text line contains annotation properity
                        annotationLine = XamlFileLines.ToList().FirstOrDefault(x => x.Contains("<Sequence"));
                    }
                    else if (CurrentWorkflow.Root.ToolboxName.Contains("Flowchart"))
                    {

                        // get the text line contains annotation properity
                        annotationLine = XamlFileLines.ToList().FirstOrDefault(x => x.Contains("<Flowchart"));

                    }

                    // Annotations check
                    if (annotationLine.Contains(":Annotation.AnnotationText="))
                    {
                        var SplitedLine = annotationLine.Split('"').ToList();

                        var annotationIndex = SplitedLine.FindIndex(x => x.Contains(":Annotation.AnnotationText=")) + 1;

                        string AnnotationValue = SplitedLine[annotationIndex];
                        /*messageList.Add(annotationIndex.ToString());
                        messageList.Add(AnnotationValue);
                        */
                        // Annotation Value is null
                        if (AnnotationValue.Equals(""))
                        {
                            // Error message existed
                            messageList.Add(string.Format("The following workflow: {0} does not have any annotations.", CurrentWorkflow.DisplayName));
                        }
                        //Commented Till Ghada update the framework
                        else
                        {
                            if (!((CurrentWorkflow.DisplayName.ToLower().Contains("_loader") || (CurrentWorkflow.DisplayName.ToLower().Contains("_worker")) || (CurrentWorkflow.DisplayName.ToLower().Contains("_loaderworker")) )))
                            {

                                string DescLine = "", ComponentName = "", PreCondLine = "", PostCondLine = "", PDDSectionLine = "", InputsLine = "", OutputsLine = "";
                                var AnnotationValueList = AnnotationValue.Split(new String[] { "&#xD;&#xA;", "&#xA;" }, System.StringSplitOptions.None);
                                if (AnnotationValue.ToLower().Contains("component name:"))
                                {
                                    ComponentName = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("component name:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(ComponentName))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("Thee following workflow: {0} does not have Component Name value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Component Name value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }




                                // Description
                                if (AnnotationValue.ToLower().Contains("description:"))
                                {
                                    DescLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("description:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(DescLine))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("The following workflow: {0} does not have Description value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Description value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                                // Pre-Condition
                                if (AnnotationValue.ToLower().Contains("pre condition:"))
                                {
                                    PreCondLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("pre condition:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(PreCondLine))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("The following workflow: {0} does not have Pre Condition value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Pre Condition value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                                // Post-Condition
                                if (AnnotationValue.ToLower().Contains("post condition:"))
                                {
                                    PostCondLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("post condition:")).ToLower().Replace(" ", "").Split(':')[1];
                                    // Post-Condition
                                    if (string.IsNullOrEmpty(PostCondLine))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("The following workflow: {0} does not have Post Condition value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Post Condition value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                                // PDD Section 
                                if (AnnotationValue.ToLower().Contains("pdd section:"))
                                {
                                    PDDSectionLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("pdd section:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(PDDSectionLine))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("The following workflow: {0} does not have PDD Section value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have PDD Section value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                                // Inputs                            
                                /*  if (AnnotationValue.ToLower().Contains("inputs:") || AnnotationValue.ToLower().Contains("input:"))
                                  {
                                      InputsLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("inputs:") || x.ToLower().Contains("input:")).ToLower().Replace(" ", "").Split(':')[1];

                                      if (string.IsNullOrEmpty(InputsLine))
                                      {
                                          // Error message existed
                                          messageList.Add(string.Format("The following workflow: {0} does not have inputs value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                      }
                                  }
                                  else
                                  {
                                      messageList.Add(string.Format("The following workflow: {0} does not have inputs/input value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                  }

                                  // outputs
                                  if (AnnotationValue.ToLower().Contains("outputs:") || AnnotationValue.ToLower().Contains("output:"))
                                  {
                                      OutputsLine = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("outputs:") || x.ToLower().Contains("output:")).ToLower().Replace(" ", "").Split(':')[1];

                                      if (string.IsNullOrEmpty(OutputsLine))
                                      {
                                          // Error message existed
                                          messageList.Add(string.Format("The following workflow: {0} does not have inputs value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                      }
                                  }
                                  else
                                  {
                                      messageList.Add(string.Format("The following workflow: {0} does not have outputs/output value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                  }*/
                            }

                        }
                    }
                    else
                    {
                        // Error message existed
                        messageList.Add(string.Format("The following workflow: {0} does not have any annotations.", CurrentWorkflow.DisplayName));
                    }
                }
                else if (CurrentWorkflow.Root.ToolboxName.Contains("TryCatch"))
                {
                    // Getting the annotations line to do the logic
                    string annotationLine = null;

                    if (CurrentWorkflow.Root.ToolboxName.Contains("TryCatch"))
                    {
                        // get the text line contains annotation properity
                        annotationLine = XamlFileLines.ToList().FirstOrDefault(x => x.Contains("<TryCatch"));
                    }


                    if (annotationLine.Contains(":Annotation.AnnotationText="))
                    {
                        var SplitedLine = annotationLine.Split('"').ToList();

                        var annotationIndex = SplitedLine.FindIndex(x => x.Contains(":Annotation.AnnotationText=")) + 1;

                        string AnnotationValue = SplitedLine[annotationIndex];


                        // Annotation Value is null
                        if (AnnotationValue.Equals(""))
                        {
                            // Error message existed
                            messageList.Add(string.Format("The following workflow: {0} does not have any annotations.", CurrentWorkflow.DisplayName));
                        }
                        //Commented Till Ghada update the framework
                        else
                        {
                            if (((CurrentWorkflow.DisplayName.ToLower().Contains("_loader") || (CurrentWorkflow.DisplayName.ToLower().Contains("_worker")) | (CurrentWorkflow.DisplayName.ToLower().Contains("_loaderworker")))))
                            {
                                


                                string TemplateVersion = "", ProcessName = "", ProcessDescription = "";
                                var AnnotationValueList = AnnotationValue.Split(new String[] { "&#xD;&#xA;", "&#xA;" }, System.StringSplitOptions.None);
                                if (AnnotationValue.ToLower().Contains("template version:"))
                                {
                                    TemplateVersion = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("template version:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(TemplateVersion))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("Thee following workflow: {0} does not have Template Version value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Template Version value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                                if (AnnotationValue.ToLower().Contains("process name:"))
                                {
                                    ProcessName = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("process name:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(ProcessName))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("Thee following workflow: {0} does not have Process Name value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Process Name value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }


                                if (AnnotationValue.ToLower().Contains("process description:"))
                                {
                                    ProcessDescription = AnnotationValueList.FirstOrDefault(x => x.ToLower().Contains("process description:")).ToLower().Replace(" ", "").Split(':')[1];

                                    if (string.IsNullOrEmpty(ProcessDescription))
                                    {
                                        // Error message existed
                                        messageList.Add(string.Format("Thee following workflow: {0} does not have Process Description value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                    }
                                }
                                else
                                {
                                    messageList.Add(string.Format("The following workflow: {0} does not have Process Description value at the workflow annotation.", CurrentWorkflow.DisplayName));
                                }

                            }
                        }
                    }
                    else
                    {
                        // Error message existed
                        messageList.Add(string.Format("The following workflow: {0} does not have any annotations.", CurrentWorkflow.DisplayName));
                    }


                }


            }

            catch (Exception ex)
            {
                messageList.Add(string.Format("Exception found: {0}.", ex.ToString()));
            }

            // No Error message existed return Success
            if (!IsEmpty(messageList))
            {
                // Error message existed return the error
                return new InspectionResult()
                {
                    HasErrors = true,
                    Messages = messageList,
                    RecommendationMessage = "please consider adding annotation to the file with the agreed structure of it",
                    ErrorLevel = theNewRule.DefaultErrorLevel
                };
            }

            return new InspectionResult()
            {
                HasErrors = false
            };
        }
    }
}